using System.Diagnostics.CodeAnalysis;
using Content.Server._Stories.ARES.Interface;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hermes;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Radio;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Hermes;

public sealed partial class HermesSystem : SharedHermesSystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly ARESCoreSystem _ares = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly AresIdentitySystem _identity = default!;
    [Dependency] private readonly GunIFFSystem _iff = default!;
    [Dependency] private readonly SharedRMCPowerSystem _power = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<RadioChannelPrototype> HermesChannel = "STHermesLink";
    private static readonly EntProtoId<IFFFactionComponent> MarineFaction = "FactionMarine";
    private readonly List<EntityUid> _sessionActors = [];

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<HermesTerminalComponent>(HermesUiKey.Key, subs =>
        {
            subs.Event<HermesActionMessage>(OnAction);
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
        });
        SubscribeLocalEvent<HermesTerminalComponent, ComponentGetStateAttemptEvent>(OnTerminalStateAttempt);
        SubscribeLocalEvent<HermesRestrictedUiComponent, ComponentGetStateAttemptEvent>(OnRestrictedStateAttempt);
        SubscribeLocalEvent<HermesTerminalComponent, ComponentShutdown>(OnTerminalShutdown);
        SubscribeLocalEvent<HermesTerminalComponent, MapInitEvent>(OnTerminalMapInit);
        SubscribeLocalEvent<HermesCoreComponent, ComponentShutdown>(OnCoreShutdown);
        SubscribeLocalEvent<IdCardComponent, ComponentShutdown>(OnCardShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<SynthGenerationComponent, EntitySpokeEvent>(OnSpoke, before: [typeof(RadioSystem)]);
        InitializeSecurity();
        InitializeSensors();
    }

    private void OnOpened(Entity<HermesTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (TryCore(ent, out var core))
            Refresh(core);
        else
            SetDisconnected(ent);
        SendSession(ent, args.Actor, HermesAccess.LoggedOut, null);
    }

    private void OnTerminalMapInit(Entity<HermesTerminalComponent> ent, ref MapInitEvent args)
        => _unlinkedTerminals.Add(ent);

    private void OnClosed(Entity<HermesTerminalComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.Sessions.Remove(args.Actor, out var session) && TryCore(ent, out var core))
        {
            AddLog(core.Comp.LoginLogs, core, CardName(session.Card), Loc.GetString("stories-hermes-logged-out"));
            Refresh(core);
        }
    }

    private void OnTerminalShutdown(Entity<HermesTerminalComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent.Comp.Core, out HermesCoreComponent? core))
            core.Terminals.Remove(ent);
        ent.Comp.Sessions.Clear();
        _unlinkedTerminals.Remove(ent);
    }

    private void OnCoreShutdown(Entity<HermesCoreComponent> ent, ref ComponentShutdown args) => ResetCore(ent);

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _nextSecurityUpdate = TimeSpan.Zero;
        _dirtyCores.Clear();
        _unlinkedTerminals.Clear();
        _disconnectedDoors.Clear();
        _disconnectedSentries.Clear();
        var query = EntityQueryEnumerator<HermesCoreComponent>();
        while (query.MoveNext(out var uid, out var core))
            ResetCore((uid, core));
    }

    private void ResetCore(Entity<HermesCoreComponent> core)
    {
        foreach (var ticket in core.Comp.Tickets)
        {
            if (ticket.Status == HermesTicketStatus.Granted)
                RemoveTemporaryAccess(core, ticket.Card);
        }
        SetLockdown(core, false);
        core.Comp.Tickets.Clear();
        core.Comp.Logs.Clear();
        core.Comp.LoginLogs.Clear();
        core.Comp.NextTicketId = 1;
        core.Comp.NextVentId = 1;
        core.Comp.NextRequests.Clear();
        core.Comp.SensorCooldowns.Clear();
        core.Comp.LockdownReadyAt = TimeSpan.Zero;
        core.Comp.Iff = HermesIff.Marines;
        foreach (var device in LinkedDevices(core))
        {
            if (TryComp(device, out HermesSecurityVentComponent? vent))
            {
                vent.ReleaseAt = null;
                vent.ReadyAt = TimeSpan.Zero;
                vent.RequestedBy = null;
            }
            ApplySentry(core, device);
        }
        foreach (var terminal in core.Comp.Terminals)
        {
            if (TryComp(terminal, out HermesTerminalComponent? comp))
            {
                comp.Sessions.Clear();
                comp.Core = null;
                SetDisconnected((terminal, comp));
                _unlinkedTerminals.Add(terminal);
            }
        }
        core.Comp.Terminals.Clear();
    }

    private void OnTerminalStateAttempt(Entity<HermesTerminalComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player?.AttachedEntity is not { } actor || !TrySession(ent, actor, out _, out _, out _))
            args.Cancelled = true;
    }

    private void OnRestrictedStateAttempt(Entity<HermesRestrictedUiComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player?.AttachedEntity is not { } actor ||
            !TryComp(ent, out HermesTerminalComponent? terminal) ||
            !TrySession((ent.Owner, terminal), actor, out _, out _, out var level) || level < HermesAccess.Certified)
            args.Cancelled = true;
    }

    private bool TryCore(Entity<HermesTerminalComponent> terminal, out Entity<HermesCoreComponent> core)
    {
        core = default;
        if (terminal.Comp.Core is not { } uid || TerminatingOrDeleted(uid) || !HasComp<HermesCoreComponent>(uid))
        {
            if (!_ares.TryGetARES(terminal.Comp.Faction, out var ares) ||
                !TryComp(ares.Value, out HermesCoreComponent? comp))
                return false;
            uid = ares.Value.Owner;
            terminal.Comp.Core = uid;
            comp.Terminals.Add(terminal);
        }
        core = (uid, Comp<HermesCoreComponent>(uid));
        return IsOnline(core);
    }

    public bool IsOnline(Entity<HermesCoreComponent> core)
    {
        if (TerminatingOrDeleted(core) ||
            TryComp(core, out ARESCoreComponent? ares) && !ares.Active ||
            !_power.IsPowered(core.Owner))
            return false;
        var processor = false;
        foreach (var device in LinkedDevices(core))
        {
            if (!HasComp<HermesProcessorComponent>(device))
                continue;
            processor = true;
            if (!_power.IsPowered(device))
                return false;
        }
        return !core.Comp.RequireProcessor || processor;
    }

    private bool TryIdentity(Entity<HermesTerminalComponent> terminal, EntityUid actor,
        out Entity<IdCardComponent> card, [NotNullWhen(true)] out AccessComponent? access)
    {
        card = default;
        access = default!;
        return !HasComp<XenoComponent>(actor) && (terminal.Comp.Portable || _power.IsPowered(terminal)) &&
            _blocker.CanInteract(actor, terminal) &&
            _idCard.TryFindIdCard(actor, out card) && !string.IsNullOrWhiteSpace(card.Comp.FullName) &&
            _iff.HasFaction(card, terminal.Comp.Faction) && TryComp(card, out access);
    }

    private bool TrySession(Entity<HermesTerminalComponent> terminal, EntityUid actor,
        out Entity<HermesCoreComponent> core, [NotNullWhen(true)] out HermesSession? session, out HermesAccess level)
    {
        core = default;
        session = null;
        level = HermesAccess.LoggedOut;
        if (!_ui.IsUiOpen(terminal.Owner, HermesUiKey.Key, actor) ||
            !terminal.Comp.Sessions.TryGetValue(actor, out session) || !TryCore(terminal, out core) ||
            !TryIdentity(terminal, actor, out var card, out var access) || card.Owner != session.Card)
            return false;
        level = GetCardAccess(core.Comp, card, access, IsWorkingJoe(actor));
        return level != HermesAccess.LoggedOut;
    }

    private HermesAccess GetCardAccess(HermesCoreComponent core, Entity<IdCardComponent> card, AccessComponent access, bool joe)
        => GetAccess(core, access, joe, _identity.TryGetJob(card, out var job) && job == core.ChiefEngineerJob);

    private void OnAction(Entity<HermesTerminalComponent> ent, ref HermesActionMessage args)
    {
        if (!_ui.IsUiOpen(ent.Owner, HermesUiKey.Key, args.Actor) || !TryCore(ent, out var core))
        {
            SetDisconnected(ent);
            return;
        }
        if (args.Action == HermesAction.Login)
        {
            if (!TryIdentity(ent, args.Actor, out var card, out var access))
                return;
            ent.Comp.Sessions[args.Actor] = new HermesSession(card);
            AddLog(core.Comp.LoginLogs, core, CardName(card), Loc.GetString("stories-hermes-logged-in"));
            Refresh(core);
            return;
        }
        if (args.Action == HermesAction.Logout)
        {
            if (ent.Comp.Sessions.Remove(args.Actor, out var previous))
                AddLog(core.Comp.LoginLogs, core, CardName(previous.Card), Loc.GetString("stories-hermes-logged-out"));
            SendSession(ent, args.Actor, HermesAccess.LoggedOut, null);
            Refresh(core);
            return;
        }
        if (!TrySession(ent, args.Actor, out core, out var session, out var level))
        {
            ent.Comp.Sessions.Remove(args.Actor);
            SendSession(ent, args.Actor, HermesAccess.LoggedOut, null);
            return;
        }
        switch (args.Action)
        {
            case HermesAction.OpenAresLogs:
                _ui.OpenUi(ent.Owner, AresUiKey.Logs, args.Actor);
                break;
            case HermesAction.ToggleSound:
                ent.Comp.NotificationsEnabled = !ent.Comp.NotificationsEnabled;
                break;
            case HermesAction.Report:
            case HermesAction.RequestAccess:
                CreateTicket(core, args.Actor, session, level, args);
                break;
            case HermesAction.ReleaseGas:
            case HermesAction.Lockdown:
            case HermesAction.ChangeIff:
                SecurityAction(core, args.Actor, level, args);
                break;
            default:
                UpdateTicket(core, args.Actor, session.Card, level, args);
                break;
        }
        Refresh(core);
    }

    private void SendSession(Entity<HermesTerminalComponent> ent, EntityUid actor, HermesAccess level, HermesSession? session)
    {
        if (session != null)
            session.Access = level;
        _ui.ServerSendUiMessage(ent.Owner, HermesUiKey.Key,
            new HermesSessionMessage(level, session == null ? string.Empty : CardName(session.Card),
                session == null ? null : GetNetEntity(session.Card), ent.Comp.NotificationsEnabled, ent.Comp.Connected), actor);
    }

    private string CardName(EntityUid card) => CompOrNull<IdCardComponent>(card)?.FullName ??
        (Deleted(card) ? Loc.GetString("stories-hermes-unknown-card") : Name(card));

    private NetEntity? CardNet(EntityUid? card) => card is { } uid && !Deleted(uid) ? GetNetEntity(uid) : null;

    private void OnCardShutdown(Entity<IdCardComponent> ent, ref ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<HermesCoreComponent>();
        while (query.MoveNext(out var uid, out var core))
        {
            foreach (var ticket in core.Tickets)
            {
                if (ticket.Card == ent.Owner && !IsClosed(ticket.Status))
                    ticket.Status = ticket.Status == HermesTicketStatus.Granted ? HermesTicketStatus.Revoked : HermesTicketStatus.Cancelled;
                if (ticket.AssigneeCard == ent.Owner && CanResolve(ticket.Status))
                {
                    ticket.AssigneeCard = null;
                    ticket.Assignee = null;
                    ticket.Status = HermesTicketStatus.Pending;
                }
            }
            core.NextRequests.Remove(ent);
            _dirtyCores.Add(uid);
        }
    }

    private void AddLog(List<HermesLogEntry> logs, Entity<HermesCoreComponent> core, string author, string text)
    {
        while (logs.Count >= core.Comp.MaxLogs && logs.Count > 0)
            logs.RemoveAt(0);
        logs.Add(new HermesLogEntry(_ticker.RoundDuration(), author, text));
    }

    private void Announce(Entity<HermesCoreComponent> core, string message)
    {
        AddLog(core.Comp.Logs, core, "HERMES", message);
        _radio.SendRadioMessage(core, message, HermesChannel, core);
        Notify(core);
    }

    private void Notify(Entity<HermesCoreComponent> core)
    {
        foreach (var terminal in core.Comp.Terminals)
        {
            if (!TryComp(terminal, out HermesTerminalComponent? comp))
                continue;
            if (comp.NotificationsEnabled && IsOnline(core))
                _audio.PlayPvs(core.Comp.NotificationSound, terminal);
        }
    }

    private void OnSpoke(Entity<SynthGenerationComponent> ent, ref EntitySpokeEvent args)
    {
        if (!IsWorkingJoe(ent) || args.Channel is not { } channel || channel.ID != HermesChannel.Id)
            return;
        if (!_ares.TryGetARES(MarineFaction, out var ares) ||
            !TryComp(ares.Value, out HermesCoreComponent? core) || !IsOnline((ares.Value, core)))
        {
            args.Channel = null;
            return;
        }
        AddLog(core.Logs, (ares.Value, core), Name(ent), args.Message);
        Refresh((ares.Value, core));
    }

    private void SetDisconnected(Entity<HermesTerminalComponent> ent)
    {
        ent.Comp.Connected = false;
        ent.Comp.Tickets = [];
        Dirty(ent);
        foreach (var actor in _ui.GetActors(ent.Owner, HermesUiKey.Key))
            SendSession(ent, actor, HermesAccess.LoggedOut, null);
    }

    private void Refresh(Entity<HermesCoreComponent> core)
    {
        var updated = new HermesCoreUpdatedEvent(core);
        RaiseLocalEvent(ref updated);
        core.Comp.LastOnline = IsOnline(core);
        core.Comp.Terminals.RemoveWhere(terminal => TerminatingOrDeleted(terminal));
        foreach (var uid in core.Comp.Terminals)
        {
            if (!TryComp(uid, out HermesTerminalComponent? terminal))
                continue;
            terminal.Connected = IsOnline(core);
            if (!_ui.IsUiOpen(uid, HermesUiKey.Key))
                continue;
            terminal.MaxDetailLength = core.Comp.MaxDetailLength;
            terminal.Tickets = [];
            foreach (var ticket in core.Comp.Tickets)
            {
                terminal.Tickets.Add(TicketView(ticket));
            }
            Dirty(uid, terminal);
            RefreshSecurity(core, uid);
            _sessionActors.Clear();
            _sessionActors.AddRange(terminal.Sessions.Keys);
            foreach (var actor in _sessionActors)
            {
                var valid = TrySession((uid, terminal), actor, out _, out var session, out var level);
                if (!valid)
                    terminal.Sessions.Remove(actor);
                SendSession((uid, terminal), actor, valid ? level : HermesAccess.LoggedOut, valid ? session : null);
            }
        }
    }
}
