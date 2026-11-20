// ReSharper disable CheckNamespace
using System.Linq;
using Content.Server._Stories.DistressSignal;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Rules;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Rules.DistressSignal;

public sealed partial class CMDistressSignalRuleSystem
{
    private StoriesDistressSignalStore? _persistenceStore;
    private bool _applyingPersistedBalance;

    private void InitializePersistence()
    {
        _persistenceStore = new StoriesDistressSignalStore(_distressResources.UserData, _marinesPerXeno, _mapVoteExcludeLast);
        foreach (var error in _persistenceStore.RecoveryErrors)
            Log.Error($"Distress Signal local recovery: {error}. Gameplay continues with the recovered state.");
        if (_persistenceStore.HasSnapshot)
            ApplyLoadedPersistence(_persistenceStore.State);
        FlushLocalPersistence();
    }

    public override void Shutdown()
    {
        if (_persistenceStore != null && !_persistenceStore.ShutdownAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult())
            Log.Error("Distress Signal could not finish its final save within the shutdown limit or storage is unavailable.");
        ReportPersistenceWriteError();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        FlushLocalPersistence();
    }

    private void FlushLocalPersistence()
    {
        _persistenceStore?.FlushAsync();
        ReportPersistenceWriteError();
    }

    private void ReportPersistenceWriteError()
    {
        if (_persistenceStore?.TakeWriteError() is { } error)
            Log.Error($"Distress Signal cannot save local state; continuing in memory, retry in 30s: {error.Message}");
    }

    private void RecordLocalPersistence(Action<StoriesDistressSignalStore> update)
    {
        if (_persistenceStore == null)
            return;
        try
        {
            update(_persistenceStore);
            FlushLocalPersistence();
        }
        catch (Exception e)
        {
            Log.Error($"Distress Signal could not record a local operation; gameplay continues: {e.Message}");
        }
    }

    private void ApplyLoadedPersistence(StoriesDistressSignalState state)
    {
        ReplaceRecentPlanets(state.RecentPlanetIds);
        var allPlanets = _rmcPlanet.GetAllPlanets();
        var allPlanetIds = allPlanets.Select(p => p.Proto.ID).ToHashSet();
        _carryoverVotes.Clear();
        foreach (var (planetId, votes) in state.CarryoverVotes.Where(v => v.Value > 0 && allPlanetIds.Contains(v.Key)))
            _carryoverVotes[new EntProtoId<RMCPlanetMapPrototypeComponent>(planetId)] = votes;
        allPlanets.TryFirstOrNull(p => p.Proto.ID == state.SelectedPlanetId, out var selected);
        if (state.SelectedPlanetId != null && selected == null)
        {
            var candidates = _rmcPlanet.GetCandidatesInRotation();
            if (candidates.Count > 0)
                selected = _random.Pick(candidates);
        }
        if (GameTicker.RunLevel == GameRunLevel.PreRoundLobby)
            SelectedPlanetMap = selected;
        ApplyPersistedBalance(state.MarinesPerXeno);
    }

    private void OnPersistenceRoundStarting(RoundStartingEvent ev)
    {
        if (TryGetActiveRuleEntity() != null)
            SelectRandomPlanet();
    }

    private void OnMarinesPerXenoChanged(float value)
    {
        _marinesPerXeno = value;
        if (_applyingPersistedBalance || !float.IsFinite(value) || value <= 0)
            return;
        RecordLocalPersistence(store => store.SetBalance(value));
    }

    private void ApplyPersistedBalance(float value)
    {
        _applyingPersistedBalance = true;
        try
        {
            _config.SetCVar(RMCCVars.CMMarinesPerXeno, value);
            _marinesPerXeno = value;
        }
        finally { _applyingPersistedBalance = false; }
    }

    private void OnMapVoteExcludeLastChanged(int value)
    {
        _mapVoteExcludeLast = Math.Clamp(value, 0, StoriesDistressSignalStore.MaxRecentPlanets);
        if (_persistenceStore != null)
        {
            RecordLocalPersistence(store => store.SetRecentPlanetCount(_mapVoteExcludeLast));
            ReplaceRecentPlanets(_persistenceStore.State.RecentPlanetIds);
        }
        else
            TrimRecentPlanets();
    }

    private void ReplaceRecentPlanets(IEnumerable<string> planetIds)
    {
        _lastPlanetMaps.Clear();
        foreach (var planetId in planetIds)
            _lastPlanetMaps.Enqueue(new EntProtoId<RMCPlanetMapPrototypeComponent>(planetId));
        TrimRecentPlanets();
    }

    private void TrackPlayedPlanet(EntProtoId<RMCPlanetMapPrototypeComponent> planetId)
    {
        _lastPlanetMaps.Enqueue(planetId);
        TrimRecentPlanets();
        RecordLocalPersistence(store => store.RecordPlayedPlanet(planetId.Id));
    }

    private void TrimRecentPlanets()
    {
        while (_lastPlanetMaps.Count > _mapVoteExcludeLast)
            _lastPlanetMaps.Dequeue();
    }

    private void FinishPersistentRound(int roundId, float value)
    {
        try
        {
            if (_persistenceStore == null || !_persistenceStore.FinalizeRound(roundId, value))
                return;
        }
        catch (Exception e)
        {
            Log.Error($"Distress Signal could not finalize local round state: {e.Message}");
            return;
        }
        ApplyPersistedBalance(_persistenceStore.State.MarinesPerXeno);
        FlushLocalPersistence();
    }

    private void PersistVotingState(RMCPlanet? selectedPlanet,
        IReadOnlyDictionary<EntProtoId<RMCPlanetMapPrototypeComponent>, int> carryoverVotes, string? announcement = null)
    {
        var votes = carryoverVotes.Where(v => v.Value > 0).ToDictionary(v => v.Key.Id, v => v.Value);
        _carryoverVotes.Clear();
        foreach (var (planetId, count) in votes)
            _carryoverVotes[new EntProtoId<RMCPlanetMapPrototypeComponent>(planetId)] = count;
        SelectedPlanetMap = selectedPlanet;
        RecordLocalPersistence(store => store.SetVotingState(selectedPlanet?.Proto.ID, votes));
        if (announcement != null)
            _chatManager.ChatMessageToAll(ChatChannel.Server, announcement, announcement, EntityUid.Invalid, hideChat: false, recordReplay: true);
    }
}
