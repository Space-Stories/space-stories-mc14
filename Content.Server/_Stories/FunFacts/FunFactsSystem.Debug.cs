using System.Linq;

namespace Content.Server._Stories.FunFacts;

public sealed partial class FunFactsSystem
{
    public List<string> GetDebugReport()
    {
        var lines = new List<string>();
        var query = EntityQueryEnumerator<FunFactsTrackerComponent>();
        while (query.MoveNext(out var uid, out var tracker))
        {
            var connected = tracker.Player is { } player && IsConnected(player);
            lines.Add($"{ToPrettyString(uid)} player={tracker.Player} connected={connected} xeno={tracker.Xeno} " +
                      $"excludedCaste={tracker.ExcludedCaste} dead={tracker.Dead} transferred={tracker.Transferred} | {FormatStats(tracker.Stats)}");
        }

        foreach (var entry in _departed)
        {
            lines.Add($"[departed] {entry.Name} player={entry.Player} xeno={entry.Xeno} excludedCaste={entry.ExcludedCaste} " +
                      $"dead={entry.Dead} | {FormatStats(entry.Stats)}");
        }

        foreach (var (userId, player) in _players)
        {
            lines.Add($"[player] {userId} name={player.Name} xeno={player.Xeno} deaths={player.Deaths} larvae={player.LarvaeBurst}");
        }

        return lines;
    }

    private static string FormatStats(FunFactsStats stats)
    {
        var values = typeof(FunFactsStats)
            .GetFields()
            .Select(field => (field.Name, Value: field.GetValue(stats)))
            .Where(pair => pair.Value is not (0 or 0f) && !TimeSpan.Zero.Equals(pair.Value))
            .Select(pair => $"{pair.Name}={pair.Value}");

        var result = string.Join(", ", values);
        return result.Length == 0 ? "-" : result;
    }
}
