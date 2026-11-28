using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Destructible;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.RMCClock;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Doors.Components;
using Content.Shared.Doors;
using Content.Shared.Ghost;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.TextScreen;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.MilitaryPolice;

/// <summary>
/// Executes issued JAS sentences. Release remains a separate MP action, including after a pardon.
/// </summary>
public sealed class RMCBrigCellSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _announce = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly RMCClockSystem _clock = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly DeviceLinkSystem _links = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RMCPoliceSystem _police = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCPowerSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly HashSet<EntityUid> _refreshQueue = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RMCBrigCellComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RMCBrigCellComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RMCBrigCellComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<RMCBrigCellComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RMCBrigCellComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<RMCBrigCellComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<RMCBrigCellComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<RMCBrigCellComponent, BoundUserInterfaceMessageAttempt>(OnMessageAttempt);
        SubscribeLocalEvent<RMCBrigCellComponent, BreakageEventArgs>(OnBreakage);
        SubscribeLocalEvent<RMCBrigCellComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<RMCBrigCellComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<RMCBrigCellComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<DeviceLinkSinkComponent, DoorStateChangedEvent>(OnDoorStateChanged);
        SubscribeLocalEvent<RMCBrigCellUiComponent, ComponentGetStateAttemptEvent>(OnUiGetStateAttempt);
        Subs.BuiEvents<RMCBrigCellComponent>(RMCBrigCellUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<RMCBrigCellMsg>(OnMessage);
        });
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args) => _refreshQueue.Clear();

    private void OnPowerChanged(Entity<RMCBrigCellComponent> ent, ref PowerChangedEvent args)
        => _refreshQueue.Add(ent);

    private void OnNewLink(Entity<RMCBrigCellComponent> ent, ref NewLinkEvent args)
        => _refreshQueue.Add(ent);

    private void OnPortDisconnected(Entity<RMCBrigCellComponent> ent, ref PortDisconnectedEvent args)
        => _refreshQueue.Add(ent);

    private void OnDoorStateChanged(Entity<DeviceLinkSinkComponent> ent, ref DoorStateChangedEvent args)
    {
        foreach (var source in ent.Comp.LinkedSources)
        {
            if (HasComp<RMCBrigCellComponent>(source))
                _refreshQueue.Add(source);
        }
    }

    private void OnInit(Entity<RMCBrigCellComponent> ent, ref ComponentInit args)
    {
        _containers.EnsureContainer<Container>(ent, RMCBrigCellComponent.ReportContainer);
        _links.EnsureSourcePorts(ent, RMCBrigCellComponent.StartPort, RMCBrigCellComponent.OpenPort,
            RMCBrigCellComponent.ClosePort);
        Refresh(ent);
    }

    private void OnShutdown(Entity<RMCBrigCellComponent> ent, ref ComponentShutdown args)
    {
        _refreshQueue.Remove(ent);
        Pause(ent);
    }

    private void OnUnpaused(Entity<RMCBrigCellComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.StartedAt += args.PausedTime;
        ent.Comp.FinishAt += args.PausedTime;
        Refresh(ent);
    }

    private void OnBreakage(Entity<RMCBrigCellComponent> ent, ref BreakageEventArgs args)
    {
        _ui.CloseUis(ent.Owner);
        _refreshQueue.Add(ent);
    }

    private bool Operable(EntityUid uid)
    {
        return !(CompOrNull<DestructibleComponent>(uid)?.IsBroken ?? false) && _power.IsPowered(uid);
    }

    private bool CanUse(EntityUid controller, EntityUid actor)
    {
        var canGhostInteract = TryComp<GhostComponent>(actor, out var ghost) && ghost.CanGhostInteract;
        return Operable(controller) && _access.IsAllowed(actor, controller) &&
               (canGhostInteract || _mobState.IsAlive(actor)) &&
               _actionBlocker.CanInteract(actor, controller) &&
               _interaction.InRangeUnobstructed(actor, controller);
    }

    private void OnOpenAttempt(Entity<RMCBrigCellComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanUse(ent, args.User))
            args.Cancel();
    }

    private void OnMessageAttempt(Entity<RMCBrigCellComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!CanUse(ent, args.Actor))
            args.Cancel();
    }

    private void OnOpened(Entity<RMCBrigCellComponent> ent, ref BoundUIOpenedEvent args) => Refresh(ent);

    private void OnUiGetStateAttempt(Entity<RMCBrigCellUiComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player?.AttachedEntity is not { } actor || !_ui.IsUiOpen(ent.Owner, RMCBrigCellUiKey.Key, actor) ||
            !CanUse(ent.Owner, actor))
            args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<RMCBrigCellComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !CanUse(ent, args.User) || !HasComp<PaperComponent>(args.Used))
            return;
        args.Handled = true;

        if (!TryComp(args.Used, out RMCIncidentReportPaperComponent? paper) || paper.Report is not { } report ||
            report.FinalSentence is not { } sentence || !IsConfinement(sentence))
        {
            Error(args.User, "rmc-brig-issued-report-required");
            return;
        }
        if (report.LawCatalogue != _police.LawCatalogue)
        {
            Error(args.User, "rmc-police-report-wrong-catalogue");
            return;
        }

        var container = _containers.GetContainer(ent, RMCBrigCellComponent.ReportContainer);
        if (container.ContainedEntities.Count >= ent.Comp.MaxReports)
        {
            Error(args.User, "rmc-brig-full");
            return;
        }
        if (!_containers.Insert(args.Used, container))
            return;

        // An active sentence must be checkpointed by its previous controller before transfer.
        if (report.Detention is { Status: RMCBrigSentenceStatus.Active } detention)
        {
            detention.Status = RMCBrigSentenceStatus.Paused;
            report.Detention = detention;
        }
        report.Detention ??= new RMCBrigSentence();
        ent.Comp.ViewedReport = args.Used;
        LogChange(ent, args.User, $"inserted the incident report for {report.Suspect}");
        _police.SynchronizeIncidentReport((args.Used, paper));
        Changed(ent);
        _ui.TryOpenUi(ent.Owner, RMCBrigCellUiKey.Key, args.User);
    }

    private static bool IsConfinement(RMCMarineSentence sentence)
    {
        return sentence.Punishment is RMCMarineLawPunishment.Permanent or RMCMarineLawPunishment.Capital ||
               sentence.Punishment == RMCMarineLawPunishment.Brig && float.IsFinite(sentence.BrigTime) &&
               sentence.BrigTime > 0 && sentence.BrigTime < TimeSpan.MaxValue.TotalMinutes;
    }

    private void OnInserted(Entity<RMCBrigCellComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == RMCBrigCellComponent.ReportContainer)
            Changed(ent);
    }

    private void OnRemoved(Entity<RMCBrigCellComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != RMCBrigCellComponent.ReportContainer)
            return;
        if (ent.Comp.ActiveReport == args.Entity)
            Pause(ent);
        if (ent.Comp.ViewedReport == args.Entity)
            ent.Comp.ViewedReport = args.Container.ContainedEntities.Count > 0
                ? args.Container.ContainedEntities[0] : null;
        Changed(ent);
    }

    private void OnMessage(Entity<RMCBrigCellComponent> ent, ref RMCBrigCellMsg args)
    {
        if (!CanUse(ent, args.Actor))
            return;
        if (args.Revision != ent.Comp.Revision)
        {
            Refresh(ent);
            return;
        }
        switch (args.Action)
        {
            case RMCBrigCellAction.ToggleDoors:
                var doors = GetLinkedDoors(ent);
                if (!doors.CanToggleDoors)
                    return;
                _links.InvokePort(ent, doors.DoorsOpen ? RMCBrigCellComponent.ClosePort : RMCBrigCellComponent.OpenPort);
                LogChange(ent, args.Actor, doors.DoorsOpen ? "closed the linked doors" : "opened the linked doors");
                Changed(ent);
                return;
        }

        if (args.Paper is not { } netPaper || !TryGetEntity(netPaper, out var paperUid) ||
            !_containers.GetContainer(ent, RMCBrigCellComponent.ReportContainer).Contains(paperUid.Value) ||
            !TryReport(paperUid, out var paper, out var report))
            return;

        if (args.Action == RMCBrigCellAction.Select)
        {
            ent.Comp.ViewedReport = paper.Owner;
            Changed(ent);
            return;
        }
        if (ent.Comp.ViewedReport != paper.Owner)
            return;

        var detention = report.Detention ?? new RMCBrigSentence();
        switch (args.Action)
        {
            case RMCBrigCellAction.Start:
                if (detention.Status is not (RMCBrigSentenceStatus.Ready or RMCBrigSentenceStatus.Paused))
                    return;
                Pause(ent);
                ent.Comp.ActiveReport = paper.Owner;
                ent.Comp.StartedAt = _timing.CurTime;
                ent.Comp.FinishAt = report.FinalSentence!.Value.Punishment == RMCMarineLawPunishment.Brig
                    ? _timing.CurTime + TimeSpan.FromMinutes(report.FinalSentence.Value.BrigTime) - detention.TimeServed
                    : null;
                if (ent.Comp.FinishAt != null)
                    EnsureComp<RMCActiveBrigCellComponent>(ent);
                detention.Status = RMCBrigSentenceStatus.Active;
                detention.StartedAt ??= _clock.GetWorldTime().TimeOfDay;
                report.Detention = detention;
                _links.InvokePort(ent, RMCBrigCellComponent.StartPort);
                LogChange(ent, args.Actor, $"started the sentence for {report.Suspect}");
                if (detention.TimeServed == TimeSpan.Zero)
                    Announce(ent, "rmc-brig-announcement-start", report);
                break;
            case RMCBrigCellAction.Pause:
                if (ent.Comp.ActiveReport != paper.Owner)
                    return;
                Pause(ent);
                LogChange(ent, args.Actor, $"paused the sentence for {report.Suspect}");
                break;
            case RMCBrigCellAction.End:
                if (ent.Comp.ActiveReport != paper.Owner)
                    return;
                Finish(ent, paper, report);
                LogChange(ent, args.Actor, $"ended the sentence early for {report.Suspect}");
                return;
            case RMCBrigCellAction.Reset:
                if (ent.Comp.ActiveReport == paper.Owner)
                    Pause(ent);
                report.Detention = new RMCBrigSentence();
                LogChange(ent, args.Actor, $"reset the sentence for {report.Suspect}");
                Announce(ent, "rmc-brig-announcement-reset", report);
                break;
            case RMCBrigCellAction.Pardon:
                if (detention.Status is RMCBrigSentenceStatus.Served or RMCBrigSentenceStatus.Pardoned)
                    return;
                if (!_access.FindAccessTags(args.Actor).Any(ent.Comp.PardonAccess.Contains))
                {
                    Error(args.Actor, "rmc-brig-pardon-denied");
                    return;
                }
                if (_police.IsReportSubject(args.Actor, report))
                {
                    Error(args.Actor, "rmc-brig-pardon-self-denied");
                    return;
                }
                if (ent.Comp.ActiveReport == paper.Owner)
                {
                    Pause(ent);
                    detention = report.Detention!.Value;
                }
                detention.Status = RMCBrigSentenceStatus.Pardoned;
                detention.PardonedBy = Name(args.Actor);
                detention.PardonedAt = _clock.GetWorldTime().TimeOfDay;
                detention.FinishedAt = detention.PardonedAt;
                report.Detention = detention;
                LogChange(ent, args.Actor, $"pardoned {report.Suspect}", LogImpact.High);
                Announce(ent, "rmc-brig-announcement-pardon", report);
                break;
            case RMCBrigCellAction.Eject:
                if (ent.Comp.ActiveReport == paper.Owner)
                    Pause(ent);
                if (!_containers.Remove(paper.Owner, _containers.GetContainer(ent, RMCBrigCellComponent.ReportContainer)))
                    return;
                _hands.TryPickupAnyHand(args.Actor, paper);
                LogChange(ent, args.Actor, $"ejected the incident report for {report.Suspect}");
                return;
            default:
                return;
        }

        _police.SynchronizeIncidentReport(paper);
        Changed(ent);
    }

    private bool TryReport(EntityUid? uid, out Entity<RMCIncidentReportPaperComponent> paper,
        [NotNullWhen(true)] out RMCIncidentReport? report)
    {
        paper = default;
        report = null;
        if (!TryComp(uid, out RMCIncidentReportPaperComponent? component) ||
            component.Report is not { FinalSentence: { } sentence } incident || !IsConfinement(sentence))
            return false;
        paper = (uid.Value, component);
        report = incident;
        return true;
    }

    private TimeSpan Elapsed(Entity<RMCBrigCellComponent> ent, RMCIncidentReport report)
    {
        return (report.Detention ?? new RMCBrigSentence()).GetTimeServed(_timing.CurTime,
            ent.Comp.StartedAt, report.FinalSentence);
    }

    private void Pause(Entity<RMCBrigCellComponent> ent)
    {
        if (TryReport(ent.Comp.ActiveReport, out var paper, out var report))
        {
            var detention = report.Detention ?? new RMCBrigSentence();
            detention.TimeServed = Elapsed(ent, report);
            detention.Status = RMCBrigSentenceStatus.Paused;
            report.Detention = detention;
            _police.SynchronizeIncidentReport(paper);
        }
        ent.Comp.ActiveReport = null;
        ent.Comp.FinishAt = null;
        RemComp<RMCActiveBrigCellComponent>(ent);
    }

    private void Finish(Entity<RMCBrigCellComponent> ent, Entity<RMCIncidentReportPaperComponent> paper,
        RMCIncidentReport report)
    {
        var detention = report.Detention ?? new RMCBrigSentence();
        detention.TimeServed = Elapsed(ent, report);
        detention.Status = RMCBrigSentenceStatus.Served;
        detention.FinishedAt = _clock.GetWorldTime().TimeOfDay;
        report.Detention = detention;
        ent.Comp.ActiveReport = null;
        ent.Comp.FinishAt = null;
        RemComp<RMCActiveBrigCellComponent>(ent);
        _police.SynchronizeIncidentReport(paper);
        Announce(ent, "rmc-brig-announcement-served", report);
        Changed(ent);
    }

    private void Announce(Entity<RMCBrigCellComponent> ent, LocId message, RMCIncidentReport report)
    {
        _announce.AnnounceRadio(ent, Loc.GetString(message, ("name", report.Suspect),
            ("cell", CellName(ent)), ("sentence", report.FinalSentence!.Value.GetDescription()),
            ("officer", report.Detention?.PardonedBy ?? "")), ent.Comp.RadioChannel);
    }

    private void Changed(Entity<RMCBrigCellComponent> ent)
    {
        ent.Comp.Revision++;
        Refresh(ent);
    }

    private void Refresh(Entity<RMCBrigCellComponent> ent)
    {
        var doors = GetLinkedDoors(ent);
        ent.Comp.DoorsOpen = doors.DoorsOpen;
        ent.Comp.CanToggleDoors = doors.CanToggleDoors;
        var reports = new List<RMCBrigReportView>();
        if (_containers.TryGetContainer(ent, RMCBrigCellComponent.ReportContainer, out var container))
        {
            foreach (var uid in container.ContainedEntities)
            {
                if (TryReport(uid, out _, out var report))
                    reports.Add(new RMCBrigReportView(GetNetEntity(uid), report.Clone()));
            }
        }
        var data = EnsureComp<RMCBrigCellUiComponent>(ent);
        data.Data = new RMCBrigCellUiData(CellName(ent), reports,
            GetNetEntity(ent.Comp.ViewedReport), GetNetEntity(ent.Comp.ActiveReport), ent.Comp.FinishAt,
            ent.Comp.Revision, doors.DoorsOpen, doors.CanToggleDoors, ent.Comp.StartedAt);
        Dirty(ent.Owner, data);
        UpdateScreen(ent);
    }

    private (bool DoorsOpen, bool CanToggleDoors) GetLinkedDoors(EntityUid controller)
    {
        if (!TryComp(controller, out DeviceLinkSourceComponent? source))
            return default;

        var hasDoors = false;
        var doorsOpen = false;
        var doorsMoving = false;
        foreach (var (sink, ports) in source.LinkedPorts)
        {
            if (!ports.Any(port => port.Source == RMCBrigCellComponent.OpenPort) ||
                !ports.Any(port => port.Source == RMCBrigCellComponent.ClosePort) ||
                !TryComp(sink, out DoorComponent? door))
                continue;

            hasDoors = true;
            doorsOpen |= door.State is DoorState.Open or DoorState.Opening;
            doorsMoving |= door.State is DoorState.Opening or DoorState.Closing or DoorState.Emagging;
        }
        return (doorsOpen, hasDoors && !doorsMoving);
    }

    private string CellName(Entity<RMCBrigCellComponent> ent) => ent.Comp.CellNumber > 0
        ? Loc.GetString("rmc-brig-cell-number", ("number", ent.Comp.CellNumber)) : Name(ent);

    private void UpdateScreen(Entity<RMCBrigCellComponent> ent)
    {
        var text = "READY";
        var target = TimeSpan.Zero;
        if (!Operable(ent))
            text = string.Empty;
        else if (TryReport(ent.Comp.ActiveReport ?? ent.Comp.ViewedReport, out _, out var report))
        {
            text = report.Detention?.Status switch
            {
                RMCBrigSentenceStatus.Pardoned => "PARD",
                RMCBrigSentenceStatus.Served => "DONE",
                _ when report.FinalSentence!.Value.Punishment != RMCMarineLawPunishment.Brig => "PERM",
                _ => RMCBrigSentence.FormatTime(TimeSpan.FromMinutes(report.FinalSentence!.Value.BrigTime) -
                    (report.Detention?.TimeServed ?? TimeSpan.Zero)),
            };
            if (ent.Comp.ActiveReport != null && ent.Comp.FinishAt is { } finishAt)
            {
                target = finishAt;
                // The timer draws its own digits at the same position as the static text.
                text = string.Empty;
            }
        }
        _appearance.SetData(ent, TextScreenVisuals.DefaultText, text);
        _appearance.SetData(ent, TextScreenVisuals.ScreenText, text);
        _appearance.SetData(ent, TextScreenVisuals.TargetTime, target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var uid in _refreshQueue)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<RMCBrigCellComponent>(uid, out var cell))
                continue;
            Changed((uid, cell));
        }
        _refreshQueue.Clear();

        var query = EntityQueryEnumerator<RMCActiveBrigCellComponent, RMCBrigCellComponent>();
        while (query.MoveNext(out var uid, out _, out var cell))
        {
            // As in CM, the deadline keeps running during a power outage, and is processed on restoration.
            if (cell.FinishAt is not { } finishAt || finishAt > _timing.CurTime || !Operable(uid))
                continue;
            if (TryReport(cell.ActiveReport, out var paper, out var report))
                Finish((uid, cell), paper, report);
        }
    }

    private void Error(EntityUid actor, LocId message)
        => _popup.PopupEntity(Loc.GetString(message), actor, actor, PopupType.MediumCaution);

    private void LogChange(EntityUid controller, EntityUid actor, string message, LogImpact impact = LogImpact.Medium)
    {
        _adminLog.Add(LogType.Action, impact,
            $"{ToPrettyString(actor):player} used {ToPrettyString(controller)}: {message}");
    }
}
