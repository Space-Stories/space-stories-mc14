using Content.Server._RMC14.LinkAccount;
using Content.Server._RMC14.Rules.DistressSignal;
using Content.Server.GameTicking;
using Content.Shared._Stories.FunFacts;
using Content.Shared._Stories.SCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Stories.FunFacts;

public sealed partial class FunFactsSystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const int SuffixVariants = 3;

    private readonly List<FunFactComponent> _facts = new();
    private readonly List<FunFactsEntry> _candidates = new();
    private readonly List<FunFactsPlayer> _playerCandidates = new();

    private void InitializeReport()
    {
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend,
            after: [typeof(CMDistressSignalRuleSystem)],
            before: [typeof(LinkAccountSystem)]);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        ReloadFacts();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            ReloadFacts();
    }

    private void ReloadFacts()
    {
        _facts.Clear();
        foreach (var entity in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (entity.TryGetComponent(out FunFactComponent? fact, _compFactory))
                _facts.Add(fact);
        }
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        var entries = CollectEntries();
        var lines = new List<string>();
        AddSideFacts(entries, false, lines);
        AddSideFacts(entries, true, lines);

        if (lines.Count == 0)
            return;

        ev.AddLine(Loc.GetString("stories-fun-facts-header"));
        foreach (var line in lines)
        {
            ev.AddLine(line);
        }

        ev.AddLine(string.Empty);
    }

    private List<FunFactsEntry> CollectEntries()
    {
        var now = _timing.CurTime;
        var entries = new List<FunFactsEntry>(_departed);
        var query = EntityQueryEnumerator<FunFactsTrackerComponent>();
        while (query.MoveNext(out var uid, out var tracker))
        {
            if (tracker.Transferred || tracker.Player is not { } player)
                continue;

            if (tracker.CritStartedAt is { } critStarted)
            {
                tracker.Stats.CritTime += now - critStarted;
                tracker.CritStartedAt = now;
            }

            entries.Add(new FunFactsEntry(player, Name(uid), tracker.Gender, tracker.Xeno, tracker.ExcludedCaste, tracker.Dead, tracker.DeathCause, tracker.Stats));
        }

        return entries;
    }

    private void AddSideFacts(List<FunFactsEntry> entries, bool xeno, List<string> lines)
    {
        var facts = new List<FunFactComponent>();
        foreach (var fact in _facts)
        {
            if (xeno ? fact.Xenos : fact.Humans)
                facts.Add(fact);
        }

        _random.Shuffle(facts);

        var max = _config.GetCVar(SCCVars.FunFactsMaxPerSide);
        var added = 0;
        foreach (var fact in facts)
        {
            if (added >= max)
                break;

            var line = IsPlayerStat(fact.Stat)
                ? TryPlayerFact(fact, xeno)
                : TryEntityFact(fact, entries, xeno);

            if (line == null)
                continue;

            lines.Add(line);
            added++;
        }
    }

    private string? TryEntityFact(FunFactComponent fact, List<FunFactsEntry> entries, bool xeno)
    {
        _candidates.Clear();
        foreach (var entry in entries)
        {
            if (entry.Xeno != xeno ||
                entry.ExcludedCaste && !fact.AllowExcludedCaste ||
                GetStat(entry.Stats, fact.Stat) < fact.Min ||
                !IsConnected(entry.Player))
            {
                continue;
            }

            _candidates.Add(entry);
        }

        if (_candidates.Count == 0)
            return null;

        var picked = _random.Pick(_candidates);
        var stats = picked.Stats;
        var accuracy = stats.ShotsFired > 0 ? Math.Min(100, stats.ShotsHit * 100 / stats.ShotsFired) : 0;
        var message = Loc.GetString(fact.Message,
            ("name", FormattedMessage.EscapeText(picked.Name)),
            ("gender", picked.Gender),
            ("value", (int) GetStat(stats, fact.Stat)),
            ("accuracy", accuracy));

        var variant = _random.Next(1, SuffixVariants + 1);
        string suffix;
        if (!picked.Dead)
            suffix = Loc.GetString($"stories-fun-facts-survived-{variant}", ("gender", picked.Gender));
        else if (picked.DeathCause is { } cause)
            suffix = Loc.GetString($"stories-fun-facts-died-to-{variant}", ("gender", picked.Gender), ("cause", FormattedMessage.EscapeText(cause)));
        else
            suffix = Loc.GetString($"stories-fun-facts-died-{variant}", ("gender", picked.Gender));

        return $"{message} {suffix}";
    }

    private string? TryPlayerFact(FunFactComponent fact, bool xeno)
    {
        _playerCandidates.Clear();
        foreach (var (userId, player) in _players)
        {
            if (player.Xeno != xeno ||
                player.Name.Length == 0 ||
                GetPlayerStat(player, fact.Stat) < fact.Min ||
                !IsConnected(userId))
            {
                continue;
            }

            _playerCandidates.Add(player);
        }

        if (_playerCandidates.Count == 0)
            return null;

        var picked = _random.Pick(_playerCandidates);
        return Loc.GetString(fact.Message,
            ("name", FormattedMessage.EscapeText(picked.Name)),
            ("gender", picked.Gender),
            ("value", GetPlayerStat(picked, fact.Stat)));
    }

    private static bool IsPlayerStat(FunFactStat stat)
    {
        return stat is FunFactStat.Deaths or FunFactStat.LarvaeBurst;
    }

    private static int GetPlayerStat(FunFactsPlayer player, FunFactStat stat)
    {
        return stat switch
        {
            FunFactStat.Deaths => player.Deaths,
            FunFactStat.LarvaeBurst => player.LarvaeBurst,
            _ => 0,
        };
    }

    private static float GetStat(FunFactsStats stats, FunFactStat stat)
    {
        return stat switch
        {
            FunFactStat.Kills => stats.Kills,
            FunFactStat.DamageTaken => stats.DamageTaken,
            FunFactStat.DamageDealt => stats.DamageDealt,
            FunFactStat.FriendlyDamage => stats.FriendlyDamage,
            FunFactStat.CritMinutes => (float) stats.CritTime.TotalMinutes,
            FunFactStat.Revives => stats.Revives,
            FunFactStat.TimesRevived => stats.TimesRevived,
            FunFactStat.Evolutions => stats.Evolutions,
            FunFactStat.ShotsFired => stats.ShotsFired,
            FunFactStat.Healed => stats.Healed,
            FunFactStat.Surgeries => stats.Surgeries,
            FunFactStat.Built => stats.Built,
            FunFactStat.Dogtags => stats.Dogtags,
            FunFactStat.Grenades => stats.Grenades,
            FunFactStat.Ignited => stats.Ignited,
            FunFactStat.Eaten => stats.Eaten,
            FunFactStat.Nested => stats.Nested,
            FunFactStat.Infected => stats.Infected,
            FunFactStat.ResinBuilt => stats.ResinBuilt,
            FunFactStat.WeedsPlanted => stats.WeedsPlanted,
            FunFactStat.AcidApplied => stats.AcidApplied,
            FunFactStat.XenoHeals => stats.XenoHeals,
            FunFactStat.Knockdowns => stats.Knockdowns,
            FunFactStat.Rescues => stats.Rescues,
            _ => 0,
        };
    }
}
