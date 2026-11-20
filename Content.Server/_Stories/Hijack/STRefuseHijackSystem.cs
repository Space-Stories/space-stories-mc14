using Content.Server._RMC14.Rules.DistressSignal;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.SCCVars;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Hijack;

public sealed class STRefuseHijackSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distress = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ContainerSystem _containers = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private float _hijackMarineSqrtCoeff;

    private int _hijackMinMarinesFloor;

    private float _hijackTimerMinutes;

    public override void Initialize()
    {
        Subs.BuiEvents<DropshipNavigationComputerComponent>(DropshipHijackerUiKey.Key,
            subs => subs.Event<DropshipHijackerRefuse>(OnHijackerRefuse));

        Subs.CVar(_cfg, SCCVars.HijackMarineSqrtCoeff, v => _hijackMarineSqrtCoeff = v, true);
        Subs.CVar(_cfg, SCCVars.HijackMinMarinesFloor, v => _hijackMinMarinesFloor = v, true);
        Subs.CVar(_cfg, SCCVars.HijackTimerMinutes, v => _hijackTimerMinutes = v, true);
    }

    private int ComputeMarineThreshold()
    {
        var online = _playerManager.PlayerCount;
        var scaled = (int)Math.Round(Math.Sqrt(online) * _hijackMarineSqrtCoeff);
        return Math.Max(_hijackMinMarinesFloor, scaled);
    }

    private void OnHijackerRefuse(Entity<DropshipNavigationComputerComponent> ent,
        ref DropshipHijackerRefuse args)
    {
        var elapsed = _gameTiming.CurTime - _gameTicker.RoundStartTimeSpan;
        var remaining = TimeSpan.FromMinutes(_hijackTimerMinutes) - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            var minutes = (int)remaining.TotalMinutes;
            var seconds = remaining.Seconds;
            _popup.PopupCursor(
                Loc.GetString("st-hijack-refuse-timer", ("minutes", minutes), ("seconds", seconds)),
                args.Actor,
                PopupType.MediumCaution);
            _ui.CloseUi(ent.Owner, DropshipHijackerUiKey.Key, args.Actor);
            return;
        }

        var marinesOnPlanet = 0;
        var marineQuery = EntityQueryEnumerator<MarineComponent, MobStateComponent, TransformComponent, ActorComponent>();
        while (marineQuery.MoveNext(out var marineId, out _, out var mobState, out var transform, out var actor))
        {
            if (!_mobState.IsAlive(marineId, mobState))
                continue;

            if (_containers.IsEntityInContainer(marineId))
                continue;

            if (actor.PlayerSession == null)
                continue;

            if (_rmcPlanet.IsOnPlanet(transform))
                marinesOnPlanet++;
        }

        var threshold = ComputeMarineThreshold();

        if (marinesOnPlanet >= threshold)
        {
            _popup.PopupCursor(
                Loc.GetString("st-hijack-refuse-marines"),
                args.Actor,
                PopupType.MediumCaution);
            return;
        }

        _ui.CloseUi(ent.Owner, DropshipHijackerUiKey.Key, args.Actor);

        CMDistressSignalRuleComponent? distress = null;
        var distressQuery = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        while (distressQuery.MoveNext(out _, out var comp))
        {
            if (comp.Hijack)
                continue;

            distress = comp;
            break;
        }

        if (distress == null)
            return;

        _distress.EndRound(distress, DistressSignalRuleResult.MinorXenoVictory,
            "st-distress-signal-minorxenovictory-refuse");

        var dropshipNavigationQuery = EntityQueryEnumerator<DropshipNavigationComputerComponent>();
        while (dropshipNavigationQuery.MoveNext(out var uid, out _))
        {
            RemCompDeferred<DropshipNavigationComputerComponent>(uid);
        }

        var dropshipTerminalQuery = EntityQueryEnumerator<DropshipTerminalComponent>();
        while (dropshipTerminalQuery.MoveNext(out var uid, out _))
        {
            RemCompDeferred<DropshipTerminalComponent>(uid);
        }
    }
}
