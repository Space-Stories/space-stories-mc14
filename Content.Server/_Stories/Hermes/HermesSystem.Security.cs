using System.Linq;
using Content.Server._Stories.ARES;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Sentry;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared._Stories.Hermes;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Hermes;

public sealed partial class HermesSystem
{
    [Dependency] private readonly RMCAlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly SharedSentryTargetingSystem _sentryTargeting = default!;
    [Dependency] private readonly StoriesThreatAutoDeployerSystem _threatDeployer = default!;

    private static readonly ProtoId<SourcePortPrototype> SecurityPort = "STHermesSecurity";
    private static readonly EntProtoId<ARESLogTypeComponent> SecurityLog = "STARESTabSecurityLogs";
    private TimeSpan _nextSecurityUpdate;
    private readonly HashSet<EntityUid> _dirtyCores = [];
    private readonly HashSet<EntityUid> _unlinkedTerminals = [];
    private readonly HashSet<EntityUid> _disconnectedDoors = [];

    private void InitializeSecurity()
    {
        SubscribeLocalEvent<HermesCoreComponent, NewLinkEvent>(OnSecurityLinked);
        SubscribeLocalEvent<HermesCoreComponent, LinkAttemptEvent>(OnSecurityLinkAttempt);
        SubscribeLocalEvent<HermesCoreComponent, PortDisconnectedEvent>(OnCoreDisconnected);
        SubscribeLocalEvent<HermesLockdownDoorComponent, BeforeDoorOpenedEvent>(OnDoorOpening);
        SubscribeLocalEvent<HermesLockdownDoorComponent, PortDisconnectedEvent>(OnDoorDisconnected);
        SubscribeLocalEvent<HermesSentryComponent, PortDisconnectedEvent>(OnSentryDisconnected);
    }

    private IEnumerable<EntityUid> LinkedDevices(EntityUid core)
    {
        if (TryComp(core, out DeviceLinkSourceComponent? source))
        {
            var outputs = source.Outputs;
            if (outputs.TryGetValue(SecurityPort, out var devices))
                return devices;
        }
        return Array.Empty<EntityUid>();
    }

    private void OnSecurityLinked(Entity<HermesCoreComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != SecurityPort || args.Source != ent.Owner)
            return;
        RegisterSecurityDevice(ent, args.Sink);
        ApplyDoor(ent, args.Sink);
        ApplySentry(ent, args.Sink);
        Refresh(ent);
    }

    private void OnSecurityLinkAttempt(Entity<HermesCoreComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.SourcePort != SecurityPort || !TryComp(args.Sink, out DeviceLinkSinkComponent? sink))
            return;
        foreach (var source in sink.LinkedSources)
        {
            if (source != ent.Owner && HasComp<HermesCoreComponent>(source))
                args.Cancel();
        }
    }

    private void OnCoreDisconnected(Entity<HermesCoreComponent> ent, ref PortDisconnectedEvent args)
        => _dirtyCores.Add(ent);

    private void RegisterSecurityDevice(Entity<HermesCoreComponent> core, EntityUid device)
    {
        if (TerminatingOrDeleted(device))
            return;
        if (TryComp(device, out HermesSecurityVentComponent? vent))
        {
            vent.Core = core;
            vent.Label ??= Loc.GetString("stories-hermes-vent-label", ("id", core.Comp.NextVentId++));
        }
    }

    private string VentName(EntityUid vent, HermesSecurityVentComponent comp) => comp.Label ?? Name(vent);

    private void OnDoorOpening(Entity<HermesLockdownDoorComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (ent.Comp.Locked)
            args.Cancel();
    }

    private void OnDoorDisconnected(Entity<HermesLockdownDoorComponent> ent, ref PortDisconnectedEvent args)
    {
        _disconnectedDoors.Add(ent);
    }

    public void SecurityAction(Entity<HermesCoreComponent> core, EntityUid actor, HermesAccess level, HermesActionMessage args)
    {
        if (level < HermesAccess.Joe || !IsOnline(core))
            return;
        switch (args.Action)
        {
            case HermesAction.ReleaseGas:
                if (args.Target is not { } target || !TryGetEntity(target, out var vent) ||
                    !LinkedDevices(core).Contains(vent.Value) ||
                    !TryComp(vent, out HermesSecurityVentComponent? gas) || !VentReady(vent.Value, gas))
                    return;
                gas.ReadyAt = _timing.CurTime + gas.Cooldown;
                gas.ReleaseAt = _timing.CurTime + gas.WarningTime;
                gas.RequestedBy = actor;
                gas.Core = core;
                _audio.PlayPvs(gas.WarningSound, vent.Value);
                _popup.PopupEntity(Loc.GetString("stories-hermes-vent-hissing", ("vent", vent.Value)), vent.Value);
                Announce(core, Loc.GetString("stories-hermes-gas-warning", ("vent", VentName(vent.Value, gas))));
                _adminLog.Add(LogType.Action, LogImpact.High,
                    $"HERMES: {ToPrettyString(actor):user} requested CN20-X release from {ToPrettyString(vent.Value):target}");
                break;
            case HermesAction.Lockdown:
                if (_timing.CurTime < core.Comp.LockdownReadyAt ||
                    !LinkedDevices(core).Any(HasComp<HermesLockdownDoorComponent>))
                    return;
                SetLockdown(core, !core.Comp.LockedDown);
                core.Comp.LockdownReadyAt = _timing.CurTime + core.Comp.LockdownCooldown;
                var announcement = Loc.GetString(core.Comp.LockedDown
                    ? "stories-hermes-lockdown-enabled" : "stories-hermes-lockdown-disabled");
                var alert = _alertLevel.Get() ?? RMCAlertLevels.Green;
                if (core.Comp.LockedDown && alert < RMCAlertLevels.Red)
                    _alertLevel.Set(RMCAlertLevels.Red, actor, false, false);
                else if (!core.Comp.LockedDown && alert == RMCAlertLevels.Red)
                    _alertLevel.Set(RMCAlertLevels.Blue, actor, false, false);
                Announce(core, announcement);
                _marineAnnounce.AnnounceARES(core, announcement, core.Comp.LockdownSound);
                _adminLog.Add(LogType.Action, LogImpact.High,
                    $"HERMES: {ToPrettyString(actor):user} set core {ToPrettyString(core):target} lockdown to {core.Comp.LockedDown}");
                break;
            case HermesAction.ChangeIff:
                if (level < HermesAccess.Technician || !core.Comp.FriendlyFactions.ContainsKey(args.Iff))
                    return;
                core.Comp.Iff = args.Iff;
                foreach (var device in LinkedDevices(core))
                    ApplySentry(core, device);
                Announce(core, Loc.GetString("stories-hermes-iff-updated", ("iff", Loc.GetString($"stories-hermes-iff-{args.Iff}"))));
                _adminLog.Add(LogType.Action, LogImpact.High,
                    $"HERMES: {ToPrettyString(actor):user} changed {ToPrettyString(core):target} sentry IFF to {args.Iff}");
                break;
            default:
                return;
        }
        _ares.CreateARESLog(SecurityLog, Loc.GetString("stories-ares-security-event", ("user", Name(actor)),
            ("action", Loc.GetString($"stories-ares-action-{args.Action}"))), core);
        Refresh(core);
    }

    private bool VentReady(EntityUid vent, HermesSecurityVentComponent comp)
        => !TerminatingOrDeleted(vent) && Transform(vent).Anchored && comp.ReleaseAt == null &&
            _timing.CurTime >= comp.ReadyAt && CompOrNull<WeldableComponent>(vent)?.IsWelded != true;

    private void SetLockdown(Entity<HermesCoreComponent> core, bool locked)
    {
        core.Comp.LockedDown = locked;
        foreach (var device in LinkedDevices(core))
        {
            ApplyDoor(core, device);
            ApplySentry(core, device);
        }
    }

    private void ApplyDoor(Entity<HermesCoreComponent> core, EntityUid device)
    {
        if (TerminatingOrDeleted(device) || !TryComp(device, out HermesLockdownDoorComponent? lockdown) ||
            !TryComp(device, out DoorComponent? door))
            return;
        lockdown.Locked = core.Comp.LockedDown;
        if (lockdown.Locked)
            _doors.StartClosing(device, door);
        else
            _doors.TryOpen(device, door);
    }

    private void ApplySentry(Entity<HermesCoreComponent> core, EntityUid device)
    {
        if (TerminatingOrDeleted(device) || !HasComp<HermesSentryComponent>(device) ||
            !core.Comp.FriendlyFactions.TryGetValue(core.Comp.Iff, out var factions))
            return;
        var control = Comp<HermesSentryComponent>(device);
        ApplySentryLockdown(core, device);
        var equipment = CompOrNull<RMCEquipmentDeployerComponent>(device);
        var deployedEntity = equipment?.DeployEntity;
        if (control.AppliedIff == core.Comp.Iff && control.AppliedEntity == deployedEntity)
            return;
        control.AppliedIff = core.Comp.Iff;
        control.AppliedEntity = deployedEntity;
        if (TryComp(device, out StoriesThreatAutoDeployerComponent? auto))
            _threatDeployer.SetFriendlyFactions((device, auto), factions);
        if (TryComp(device, out RMCEquipmentDeployerComponent? deployer) && deployer.DeployEntity is { } deployed &&
            TryGetEntity(deployed, out var sentry) && TryComp(sentry, out SentryTargetingComponent? targeting))
            _sentryTargeting.SetFriendlyFactions((sentry.Value, targeting), factions);
        else if (TryComp(device, out SentryTargetingComponent? ownTargeting))
            _sentryTargeting.SetFriendlyFactions((device, ownTargeting), factions);
    }

    private void RefreshSecurity(Entity<HermesCoreComponent> core, EntityUid terminal)
    {
        var data = EnsureComp<HermesRestrictedUiComponent>(terminal);
        var view = GetSecurityView(core);
        data.Logs = new(core.Comp.Logs);
        data.LoginLogs = new(core.Comp.LoginLogs);
        data.LockedDown = view.LockedDown;
        data.LockdownReadyAt = view.LockdownReadyAt;
        data.Iff = view.Iff;
        data.Vents = view.Vents;
        data.DoorCount = view.DoorCount;
        data.SentryCount = view.SentryCount;
        Dirty(terminal, data);
    }

    public AresSecurityView GetSecurityView(Entity<HermesCoreComponent> core)
    {
        var vents = new List<HermesVentView>();
        var doors = 0;
        var sentries = 0;
        foreach (var device in LinkedDevices(core))
        {
            if (TerminatingOrDeleted(device))
                continue;
            if (TryComp(device, out HermesSecurityVentComponent? vent))
                vents.Add(new HermesVentView(GetNetEntity(device), VentName(device, vent), VentReady(device, vent),
                    vent.ReadyAt, CompOrNull<WeldableComponent>(device)?.IsWelded == true));
            if (HasComp<HermesLockdownDoorComponent>(device)) doors++;
            if (HasComp<HermesSentryComponent>(device)) sentries++;
        }
        return new AresSecurityView(core.Comp.LockedDown, core.Comp.LockdownReadyAt, core.Comp.Iff, doors, sentries, vents);
    }

    public IReadOnlyList<HermesLogEntry> GetLogs(Entity<HermesCoreComponent> core) => core.Comp.Logs;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextSecurityUpdate)
            return;
        var interval = TimeSpan.MaxValue;
        var vents = EntityQueryEnumerator<HermesSecurityVentComponent>();
        while (vents.MoveNext(out var uid, out var vent))
        {
            if (vent.ReleaseAt is { } release && _timing.CurTime >= release)
            {
                vent.ReleaseAt = null;
                if (vent.Core is { } source && TryComp(source, out HermesCoreComponent? core) &&
                    IsOnline((source, core)) && LinkedDevices(source).Contains(uid) &&
                    CompOrNull<WeldableComponent>(uid)?.IsWelded != true && Transform(uid).Anchored)
                {
                    SpawnAtPosition(vent.GasPrototype, Transform(uid).Coordinates);
                    _audio.PlayPvs(vent.ReleaseSound, uid);
                    _adminLog.Add(LogType.Action, LogImpact.High,
                        $"HERMES: {ToPrettyString(vent.RequestedBy):user} released CN20-X from {ToPrettyString(uid):target}");
                }
                vent.RequestedBy = null;
            }
            var ready = VentReady(uid, vent);
            if (ready != vent.LastReady && vent.Core is { } linked)
                _dirtyCores.Add(linked);
            vent.LastReady = ready;
        }
        _unlinkedTerminals.RemoveWhere(uid => TerminatingOrDeleted(uid));
        _unlinkedTerminals.RemoveWhere(uid =>
        {
            if (TryComp(uid, out HermesTerminalComponent? terminal) && TryCore((uid, terminal), out var core))
            {
                _dirtyCores.Add(core);
                return true;
            }
            return false;
        });
        var cores = EntityQueryEnumerator<HermesCoreComponent>();
        while (cores.MoveNext(out var uid, out var core))
        {
            if (core.StatusUpdateInterval > TimeSpan.Zero && core.StatusUpdateInterval < interval)
                interval = core.StatusUpdateInterval;
            if (core.LastOnline != IsOnline((uid, core)) ||
                core.LockdownReadyAt > TimeSpan.Zero && _timing.CurTime >= core.LockdownReadyAt)
            {
                _dirtyCores.Add(uid);
                if (_timing.CurTime >= core.LockdownReadyAt)
                    core.LockdownReadyAt = TimeSpan.Zero;
            }
            foreach (var device in LinkedDevices(uid))
            {
                RegisterSecurityDevice((uid, core), device);
                ApplySentry((uid, core), device);
            }
            foreach (var terminalUid in core.Terminals)
            {
                if (!TryComp(terminalUid, out HermesTerminalComponent? terminal))
                    continue;
                foreach (var (actor, session) in terminal.Sessions)
                {
                    if (!TrySession((terminalUid, terminal), actor, out _, out _, out var access) || session.Access != access)
                        _dirtyCores.Add(uid);
                }
            }
        }
        foreach (var door in _disconnectedDoors)
        {
            if (TerminatingOrDeleted(door) || !TryComp(door, out HermesLockdownDoorComponent? lockdown))
                continue;
            lockdown.Locked = false;
            if (!TryComp(door, out DeviceLinkSinkComponent? sink))
                continue;
            foreach (var source in sink.LinkedSources)
            {
                if (TryComp(source, out HermesCoreComponent? core) && core.LockedDown)
                    lockdown.Locked = true;
            }
        }
        _disconnectedDoors.Clear();
        RefreshDisconnectedSentries();
        _nextSecurityUpdate = _timing.CurTime + (interval == TimeSpan.MaxValue ? TimeSpan.FromSeconds(1) : interval);
        foreach (var uid in _dirtyCores)
        {
            if (!TerminatingOrDeleted(uid) && TryComp(uid, out HermesCoreComponent? core))
                Refresh((uid, core));
        }
        _dirtyCores.Clear();
    }
}
