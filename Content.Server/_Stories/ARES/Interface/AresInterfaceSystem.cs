using System.Diagnostics.CodeAnalysis;
using Content.Server._Stories.Hermes;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.ExternalTerminals;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared._Stories.Hermes;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.ARES.Interface;

public sealed partial class AresInterfaceSystem : SharedAresInterfaceSystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly ARESCoreSystem _ares = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly HermesSystem _hermes = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly AresIdentitySystem _identity = default!;
    [Dependency] private readonly GunIFFSystem _iff = default!;
    [Dependency] private readonly SharedRMCPowerSystem _power = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId WorkingJoe = "RMCARESWorker";
    private static readonly EntProtoId<IFFFactionComponent> MarineFaction = "FactionMarine";
    private TimeSpan _nextUpdate;
    private readonly HashSet<EntityUid> _dirtyCores = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AresTerminalComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
        SubscribeLocalEvent<AresTerminalComponent, AresActionMessage>(OnAction);
        SubscribeLocalEvent<AresTerminalComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<AresTerminalComponent, BoundUIClosedEvent>(OnClosed);
        Subs.BuiEvents<AresTerminalComponent>(AresUiKey.Logs,
            subs => subs.Event<RMCARESExternalShowLogs>(OnShowLogs));
        SubscribeLocalEvent<AresTerminalComponent, ComponentGetStateAttemptEvent>(OnStateAttempt);
        SubscribeLocalEvent<AresTerminalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<IdCardComponent, EntityTerminatingEvent>(OnCardTerminating);
        SubscribeLocalEvent<ARESLogUpdateEvent>(OnLogsUpdated);
        SubscribeLocalEvent<HermesCoreUpdatedEvent>(OnHermesUpdated);
        SubscribeLocalEvent<HermesAccessRequestedEvent>(OnAccessRequested);
        SubscribeLocalEvent<AresAnnouncementEvent>(OnAnnouncement);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnUiMessageAttempt(Entity<AresTerminalComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (HasComp<XenoComponent>(args.Actor))
            args.Cancel();
    }

    private void OnOpened(Entity<AresTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not AresUiKey)
            return;
        if (HasComp<XenoComponent>(args.Actor))
        {
            _ui.CloseUi(ent.Owner, args.UiKey, args.Actor);
            return;
        }
        if (ent.Comp.Operator is { } actor && actor != args.Actor && IsAresUiOpen(ent, actor))
        {
            _ui.CloseUi(ent.Owner, args.UiKey, args.Actor);
            return;
        }
        var existingSession = ent.Comp.Operator == args.Actor;
        ent.Comp.Operator = args.Actor;
        if (!existingSession)
            Logout(ent);
        if (Equals(args.UiKey, AresUiKey.Logs) && ent.Comp.Card == null)
            Login(ent, args.Actor);
        Refresh(ent);
    }

    private void OnClosed(Entity<AresTerminalComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not AresUiKey || ent.Comp.Operator != args.Actor)
            return;
        if (Equals(args.UiKey, AresUiKey.Logs))
            ClearLogViewer(ent.Comp);
        var otherKey = Equals(args.UiKey, AresUiKey.Logs) ? AresUiKey.Key : AresUiKey.Logs;
        if (_ui.IsUiOpen(ent.Owner, otherKey, args.Actor))
        {
            Refresh(ent);
            return;
        }
        Logout(ent);
        ent.Comp.Operator = null;
        Dirty(ent);
    }

    private void OnShutdown(Entity<AresTerminalComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent.Comp.Core, out AresInterfaceCoreComponent? core))
            core.Terminals.Remove(ent);
        ent.Comp.Operator = null;
    }

    private void OnStateAttempt(Entity<AresTerminalComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player?.AttachedEntity != ent.Comp.Operator || ent.Comp.Operator is not { } actor ||
            !IsAresUiOpen(ent, actor))
            args.Cancelled = true;
    }

    private void OnCardTerminating(Entity<IdCardComponent> ent, ref EntityTerminatingEvent args)
    {
        var cores = EntityQueryEnumerator<AresInterfaceCoreComponent>();
        while (cores.MoveNext(out _, out var core)) core.NextMessages.Remove(ent);
    }

    private void OnLogsUpdated(ARESLogUpdateEvent args) => _dirtyCores.Add(args.Core);
    private void OnHermesUpdated(ref HermesCoreUpdatedEvent args) => RefreshCore(args.Core);

    private void OnAnnouncement(ref AresAnnouncementEvent args)
    {
        Entity<ARESCoreComponent>? ares = null;
        if (args.Source is { } source)
            _ares.TryGetARES(source, out ares);
        if (ares == null)
            _ares.TryGetARES(MarineFaction, out ares);
        if (ares == null || !TryComp(ares.Value, out AresInterfaceCoreComponent? core))
            return;
        var page = args.Title == "rmc-bioscan-ares-announcement" ? AresPage.Bioscans : AresPage.Announcements;
        if (core.LogTypes.TryGetValue(page, out var type))
            _ares.CreateARESLog(type, args.Message, ares.Value);
    }

    private void OnRestart(RoundRestartCleanupEvent args)
    {
        _nextUpdate = TimeSpan.Zero;
        _dirtyCores.Clear();
        var cores = EntityQueryEnumerator<AresInterfaceCoreComponent>();
        while (cores.MoveNext(out _, out var core))
        {
            core.Conversations.Clear();
            core.DeletedRecords.Clear();
            core.Terminals.Clear();
            core.NextMessages.Clear();
            core.NextConversationId = 1;
            core.PrinterReadyAt = core.QuartersReadyAt = TimeSpan.Zero;
        }
        var terminals = EntityQueryEnumerator<AresTerminalComponent>();
        while (terminals.MoveNext(out var uid, out var terminal))
        {
            Logout((uid, terminal));
            terminal.Operator = terminal.Core = null;
        }
    }

    private bool TryCore(Entity<AresTerminalComponent> terminal, out Entity<AresInterfaceCoreComponent> core)
    {
        core = default;
        if (!_ares.TryGetARES(terminal.Comp.Faction, out var ares) ||
            TerminatingOrDeleted(ares.Value) || !ares.Value.Comp.Active || !_power.IsPowered(ares.Value) ||
            !TryComp(ares.Value, out AresInterfaceCoreComponent? component))
            return false;
        core = (ares.Value, component);
        terminal.Comp.Core = core;
        core.Comp.Terminals.Add(terminal);
        return true;
    }

    private bool TryIdentity(Entity<AresTerminalComponent> terminal, EntityUid actor,
        out Entity<IdCardComponent> card, [NotNullWhen(true)] out AccessComponent? access)
    {
        card = default;
        access = null;
        return !HasComp<XenoComponent>(actor) &&
            (TryComp(terminal, out HermesTerminalComponent? hermes) && hermes.Portable || _power.IsPowered(terminal)) &&
            _blocker.CanInteract(actor, terminal) &&
            _idCard.TryFindIdCard(actor, out card) && !string.IsNullOrWhiteSpace(card.Comp.FullName) &&
            _iff.HasFaction(card, terminal.Comp.Faction) && TryComp(card, out access) && access.Enabled;
    }

    private bool TrySession(Entity<AresTerminalComponent> terminal, EntityUid actor,
        out Entity<AresInterfaceCoreComponent> core, out AresAccess access)
    {
        core = default;
        access = AresAccess.LoggedOut;
        if (terminal.Comp.Operator != actor || !IsAresUiOpen(terminal, actor) ||
            !TryCore(terminal, out core) || !TryIdentity(terminal, actor, out var card, out var idAccess) ||
            card.Owner != terminal.Comp.Card)
            return false;
        var joe = TryComp(actor, out SynthGenerationComponent? generation) && generation.Generation == WorkingJoe;
        AresAccess? assigned = _identity.TryGetJob(card, out var job) && core.Comp.AssignedAccess.TryGetValue(job, out var role) ? role : null;
        access = GetAccess(core.Comp, idAccess, joe, assigned);
        return access != AresAccess.LoggedOut;
    }

    private void OnAction(Entity<AresTerminalComponent> ent, ref AresActionMessage args)
    {
        if (args.UiKey is not AresUiKey || ent.Comp.Operator != args.Actor || !IsAresUiOpen(ent, args.Actor))
            return;
        if (args.Action == AresAction.Logout)
        {
            Logout(ent);
            Refresh(ent);
            return;
        }
        if (args.Action == AresAction.Login)
        {
            Login(ent, args.Actor);
            Refresh(ent);
            return;
        }
        if (Equals(args.UiKey, AresUiKey.Logs))
            return;
        if (!TrySession(ent, args.Actor, out var core, out var level))
        {
            Logout(ent);
            Refresh(ent);
            if (args.Action == AresAction.SendMessage)
                SendMessageResult(ent, args, false);
            return;
        }
        ent.Comp.Feedback = string.Empty;
        switch (args.Action)
        {
            case AresAction.OpenLogs:
                _ui.OpenUi(ent.Owner, AresUiKey.Logs, args.Actor);
                break;
            case AresAction.SelectPage:
                if (ent.Comp.Pages.Contains(args.Page) && CanView(level, args.Page))
                {
                    ent.Comp.Page = args.Page;
                    if (args.Page == AresPage.Talk) ent.Comp.ConversationId = null;
                }
                break;
            case AresAction.Sudo:
            case AresAction.EndSudo:
                if (level != AresAccess.Technician || args.Text.Length > core.Comp.MaxTextLength)
                    break;
                ent.Comp.SudoName = args.Action == AresAction.Sudo && !string.IsNullOrWhiteSpace(args.Text) ? args.Text.Trim() : null;
                Audit(core, RealUser(ent), Loc.GetString("stories-ares-sudo-event", ("name", ent.Comp.SudoName ?? RealUser(ent))));
                break;
            case AresAction.ReleaseGas:
            case AresAction.Lockdown:
            case AresAction.ChangeIff:
                if (CanUseSecurity(level) && TryComp(core, out HermesCoreComponent? security) &&
                    (args.Action != AresAction.ChangeIff || level >= AresAccess.HighCommand))
                {
                    var action = args.Action switch
                    {
                        AresAction.ReleaseGas => HermesAction.ReleaseGas,
                        AresAction.Lockdown => HermesAction.Lockdown,
                        _ => HermesAction.ChangeIff,
                    };
                    _hermes.SecurityAction((core, security), args.Actor,
                        level >= AresAccess.HighCommand ? HermesAccess.Technician : HermesAccess.Joe,
                        new HermesActionMessage(action, target: args.Target, iff: args.Iff));
                }
                break;
            case AresAction.DeleteRecord:
            case AresAction.PrintRequisitions:
                RecordAction(ent, core, level, args);
                break;
            case AresAction.SendMessage:
            case AresAction.ClearConversation:
            case AresAction.ReadConversation:
                var sent = ConversationAction(ent, core, level, args);
                if (args.Action == AresAction.SendMessage)
                    SendMessageResult(ent, args, sent);
                break;
            default:
                EmergencyAction(ent, core, level, args);
                break;
        }
        RefreshCore(core);
    }

    private string RealUser(Entity<AresTerminalComponent> terminal)
        => CompOrNull<IdCardComponent>(terminal.Comp.Card)?.FullName ?? string.Empty;

    private void Logout(Entity<AresTerminalComponent> terminal)
    {
        var user = RealUser(terminal);
        if (terminal.Comp.Card != null && TryCore(terminal, out var core))
            Audit(core, user, Loc.GetString("stories-ares-logout-event"));
        terminal.Comp.Card = null;
        terminal.Comp.SudoName = null;
        terminal.Comp.Sudo = false;
        terminal.Comp.Access = AresAccess.LoggedOut;
        terminal.Comp.User = string.Empty;
        terminal.Comp.Pages.Clear();
        terminal.Comp.Records.Clear();
        terminal.Comp.Conversation.Clear();
        terminal.Comp.ConversationId = null;
        terminal.Comp.Security = null;
        ClearLogViewer(terminal.Comp);
        terminal.Comp.LogReferences.Clear();
        terminal.Comp.Feedback = string.Empty;
        Dirty(terminal);
    }

    private void Audit(Entity<AresInterfaceCoreComponent> core, string user, string text)
    {
        if (core.Comp.LogTypes.TryGetValue(AresPage.Access, out var log))
            _ares.CreateARESLog(log, $"{user}: {text}", core);
    }

    private void RefreshCore(EntityUid uid)
    {
        if (!TryComp(uid, out AresInterfaceCoreComponent? core))
            return;
        core.Terminals.RemoveWhere(uid => TerminatingOrDeleted(uid));
        foreach (var terminal in core.Terminals)
        {
            if (TryComp(terminal, out AresTerminalComponent? component))
                Refresh((terminal, component));
        }
    }

    private void Refresh(Entity<AresTerminalComponent> terminal)
    {
        terminal.Comp.Connected = TryCore(terminal, out var core);
        if (terminal.Comp.Operator is not { } actor || !TrySession(terminal, actor, out core, out var level))
        {
            if (terminal.Comp.Card != null) Logout(terminal);
            Dirty(terminal);
            return;
        }
        terminal.Comp.Access = level;
        if (level != AresAccess.Technician) terminal.Comp.SudoName = null;
        terminal.Comp.User = terminal.Comp.SudoName ?? RealUser(terminal);
        terminal.Comp.Sudo = terminal.Comp.SudoName != null;
        terminal.Comp.MaxTextLength = core.Comp.MaxTextLength;
        terminal.Comp.Pages = [AresPage.Talk];
        if (CanUseSecurity(level))
            terminal.Comp.Pages.Add(AresPage.CoreSecurity);
        if (!terminal.Comp.Pages.Contains(terminal.Comp.Page))
            terminal.Comp.Page = AresPage.Talk;
        terminal.Comp.Security = CanUseSecurity(level) && TryComp(core, out HermesCoreComponent? security) && _hermes.IsOnline((core, security))
            ? _hermes.GetSecurityView((core, security)) : null;
        terminal.Comp.Evacuating = _evacuation.IsEvacuationEnabled();
        RefreshRecords(terminal, core, level);
        RefreshConversation(terminal, core, level);
        RefreshLogViewer(terminal, core, actor);
        Dirty(terminal);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var terminals = EntityQueryEnumerator<AresTerminalComponent>();
        while (terminals.MoveNext(out var uid, out var terminal))
        {
            if (terminal.Operator is not { } actor) continue;
            var connected = TryCore((uid, terminal), out var core);
            var valid = TrySession((uid, terminal), actor, out _, out var access);
            if (terminal.Connected != connected || terminal.Access != (valid ? access : AresAccess.LoggedOut) ||
                connected && _dirtyCores.Contains(core) || LogAccessChanged((uid, terminal), actor))
                Refresh((uid, terminal));
        }
        _dirtyCores.Clear();
    }
}
