using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server._Stories.DistressSignal;

public sealed record StoriesDistressSignalState(float MarinesPerXeno,
    ImmutableArray<string> RecentPlanetIds, ImmutableDictionary<string, int> CarryoverVotes,
    string? SelectedPlanetId, int? LastFinalizedRoundId);

public sealed class StoriesDistressSignalStore
{
    public const int MaxRecentPlanets = 10000;
    private sealed record Envelope(int Version, string Data, string Sha256);

    private static readonly ResPath Directory = new("/distress-signal");
    private readonly IWritableDirProvider _data;
    private readonly object _writeLock = new();
    private StoriesDistressSignalState _state;
    private Task _writer = Task.CompletedTask;
    private long _generation;
    private long _stateVersion = 1;
    private long _savedVersion;
    private long _failedVersion = -1;
    private DateTime _nextWriteAttempt;
    private Exception? _writeError;
    private int _recentCount;
    private bool _stopping;
    private bool _abandonWrites;

    public StoriesDistressSignalState State { get { lock (_writeLock) return _state; } }
    public bool HasPendingWrite { get { lock (_writeLock) return _savedVersion != _stateVersion; } }
    public bool HasSnapshot { get; private set; }
    public List<string> RecoveryErrors { get; } = new();

    public StoriesDistressSignalStore(IWritableDirProvider data, float initialBalance, int recentPlanetCount = 2)
    {
        _data = data;
        _recentCount = Math.Clamp(recentPlanetCount, 0, MaxRecentPlanets);
        _state = new(initialBalance, ImmutableArray<string>.Empty, ImmutableDictionary<string, int>.Empty, null, null);
        try
        {
            if (!_data.IsDir(Directory))
                return;
            foreach (var file in Checkpoints())
            {
                try
                {
                    var generation = long.Parse(file[11..^5], CultureInfo.InvariantCulture);
                    _generation = Math.Max(_generation, generation);
                    using var stream = _data.Open(Directory / file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var envelope = JsonSerializer.Deserialize<Envelope>(stream)
                        ?? throw new InvalidDataException("Empty checkpoint");
                    if (Hash(envelope.Data) != envelope.Sha256)
                        throw new InvalidDataException("Invalid checkpoint checksum");
                    var state = ReadState(envelope);
                    Validate(state);
                    _state = state with { RecentPlanetIds = state.RecentPlanetIds.TakeLast(_recentCount).ToImmutableArray() };
                    HasSnapshot = true;
                    if (envelope.Version == 2 && _state.RecentPlanetIds.Length == state.RecentPlanetIds.Length)
                        _savedVersion = _stateVersion;
                    break;
                }
                catch (Exception e)
                {
                    RecoveryErrors.Add($"{file}: {e.Message}");
                    try { _data.Rename(Directory / file, Directory / (file + ".corrupt-" + Guid.NewGuid().ToString("N"))); }
                    catch (Exception renameError) { RecoveryErrors.Add(renameError.Message); }
                }
            }
        }
        catch (Exception e) { RecoveryErrors.Add(e.Message); }
    }

    private StoriesDistressSignalState ReadState(Envelope envelope)
    {
        if (envelope.Version == 2)
            return JsonSerializer.Deserialize<StoriesDistressSignalState>(envelope.Data)
                ?? throw new InvalidDataException("Empty state");
        if (envelope.Version != 1)
            throw new InvalidDataException("Unsupported checkpoint version");

        using var document = JsonDocument.Parse(envelope.Data);
        var root = document.RootElement;
        var rounds = root.GetProperty("Rounds").EnumerateArray();
        var recent = rounds.Select(r => r.GetProperty("PlanetId").GetString()!).TakeLast(_recentCount).ToImmutableArray();
        var finalized = rounds.Where(r => r.GetProperty("Result").ValueKind != JsonValueKind.Null)
            .Select(r => (int?) r.GetProperty("RoundId").GetInt32()).LastOrDefault();
        return new(root.GetProperty("MarinesPerXeno").GetSingle(), recent,
            root.GetProperty("CarryoverVotes").Deserialize<ImmutableDictionary<string, int>>()!,
            root.GetProperty("SelectedPlanetId").GetString(), finalized);
    }

    public void SetBalance(float value) => Update(state => state with { MarinesPerXeno = value });

    public void SetVotingState(string? selectedPlanetId, IReadOnlyDictionary<string, int> votes) =>
        Update(state => state with { SelectedPlanetId = selectedPlanetId, CarryoverVotes = votes.ToImmutableDictionary() });

    public void RecordPlayedPlanet(string planetId) => Update(state => state with
    {
        RecentPlanetIds = state.RecentPlanetIds.Add(planetId).TakeLast(_recentCount).ToImmutableArray(),
        SelectedPlanetId = null,
    });

    public void SetRecentPlanetCount(int count)
    {
        lock (_writeLock)
        {
            _recentCount = Math.Clamp(count, 0, MaxRecentPlanets);
            Update(state => state with { RecentPlanetIds = state.RecentPlanetIds.TakeLast(_recentCount).ToImmutableArray() });
        }
    }

    public bool FinalizeRound(int roundId, float balance)
    {
        lock (_writeLock)
        {
            if (roundId < 0)
                throw new ArgumentOutOfRangeException(nameof(roundId));
            if (_state.LastFinalizedRoundId >= roundId)
                return false;
            Update(state => state with { MarinesPerXeno = balance, LastFinalizedRoundId = roundId });
            return true;
        }
    }

    private void Update(Func<StoriesDistressSignalState, StoriesDistressSignalState> update)
    {
        lock (_writeLock)
        {
            if (_stopping)
                throw new InvalidOperationException("Distress Signal persistence is stopping");
            var state = update(_state);
            Validate(state);
            _state = state;
            _stateVersion++;
        }
    }

    public Task FlushAsync(bool force = false)
    {
        lock (_writeLock)
        {
            if (!_writer.IsCompleted)
                return _writer;
            if (_abandonWrites || _savedVersion == _stateVersion ||
                !force && (_stopping || DateTime.UtcNow < _nextWriteAttempt))
                return Task.CompletedTask;
            return _writer = Task.Run(WritePending);
        }
    }

    private void WritePending()
    {
        while (true)
        {
            StoriesDistressSignalState state;
            long version;
            lock (_writeLock)
            {
                if (_abandonWrites || _savedVersion == _stateVersion)
                    return;
                state = _state;
                version = _stateVersion;
            }

            try
            {
                WriteSnapshot(state);
                lock (_writeLock)
                {
                    _savedVersion = version;
                    _nextWriteAttempt = default;
                    _failedVersion = -1;
                }
            }
            catch (Exception e)
            {
                lock (_writeLock)
                {
                    _writeError = e;
                    _failedVersion = version;
                    _nextWriteAttempt = DateTime.UtcNow.AddSeconds(30);
                }
                return;
            }
        }
    }

    public Exception? TakeWriteError()
    {
        lock (_writeLock)
        {
            var error = _writeError;
            _writeError = null;
            return error;
        }
    }

    public async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        lock (_writeLock)
            _stopping = true;
        using var cancellation = new CancellationTokenSource(timeout);
        while (true)
        {
            try
            {
                await FlushAsync(force: true).WaitAsync(cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (_writeLock)
                    _abandonWrites = true;
                return false;
            }

            lock (_writeLock)
            {
                if (_savedVersion == _stateVersion)
                    return true;
                if (_failedVersion == _stateVersion || _abandonWrites)
                    return false;
            }
        }
    }

    private void WriteSnapshot(StoriesDistressSignalState state)
    {
        _data.CreateDir(Directory);
        var name = "checkpoint-" + checked(++_generation).ToString("D20", CultureInfo.InvariantCulture);
        var temporary = Directory / "checkpoint.tmp";
        var payload = JsonSerializer.Serialize(state);
        using (var stream = _data.Open(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, new Envelope(2, payload, Hash(payload)));
            if (stream is FileStream file)
                file.Flush(flushToDisk: true);
            else
                stream.Flush();
        }

        // Only the sequential writer accesses the temporary file and checkpoint generations.
        _data.Rename(temporary, Directory / (name + ".json"));
        HasSnapshot = true;
        foreach (var old in Checkpoints().Skip(2))
        {
            try { _data.Delete(Directory / old); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private string[] Checkpoints() => _data.DirectoryEntries(Directory)
        .Where(f => f.StartsWith("checkpoint-", StringComparison.Ordinal) && f.EndsWith(".json", StringComparison.Ordinal))
        .OrderDescending(StringComparer.Ordinal).ToArray();

    private static void Validate(StoriesDistressSignalState state)
    {
        if (!float.IsFinite(state.MarinesPerXeno) || state.MarinesPerXeno <= 0 ||
            state.CarryoverVotes == null || state.RecentPlanetIds.IsDefault ||
            state.RecentPlanetIds.Length > MaxRecentPlanets || state.RecentPlanetIds.Any(string.IsNullOrWhiteSpace) ||
            state.SelectedPlanetId != null && string.IsNullOrWhiteSpace(state.SelectedPlanetId) ||
            state.LastFinalizedRoundId < 0 ||
            state.CarryoverVotes.Any(v => string.IsNullOrWhiteSpace(v.Key) || v.Value < 0))
            throw new InvalidDataException("Invalid Distress Signal state");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
