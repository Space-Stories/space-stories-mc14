using System.Linq;
using Content.Client._RMC14.UserInterface.Crt;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared._RMC14.RMCClock;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.MilitaryPolice;

public sealed class RMCBrigCellWindow : RMCPoliceWindow
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public event Action<RMCBrigCellMsg>? Message;
    public Func<bool>? CanPardon;

    private RMCBrigCellUiData? _state;
    private RMCCrtLabel? _timer;
    private RMCCrtLabel? _served;
    private RMCCrtLabel? _finish;
    private readonly RMCCrtActionButton _doors;
    private bool _canPardon;
    private TimeSpan _nextPermissionRefresh;
    private float _timerRefreshAccumulator;

    public RMCBrigCellWindow() : base("rmc-brig-title")
    {
        IoCManager.InjectDependencies(this);
        _doors = Button("rmc-brig-open-doors", () => Send(RMCBrigCellAction.ToggleDoors));
        Toolbar.AddChild(_doors);
    }

    public void Update(RMCBrigCellUiData state, bool canPardon)
    {
        _state = state;
        _doors.Text = Loc.GetString(state.DoorsOpen ? "rmc-brig-close-doors" : "rmc-brig-open-doors");
        _doors.Disabled = !state.CanToggleDoors;
        Title = state.CellName;
        _canPardon = canPardon;
        _timer = null;
        _served = null;
        _finish = null;
        Left.DisposeAllChildren();
        Right.DisposeAllChildren();
        Left.AddChild(Heading("rmc-brig-reports"));
        Left.AddChild(Text(Loc.GetString("rmc-brig-insert-hint"), RMCCrtTone.Muted));

        foreach (var view in state.Reports)
        {
            var status = view.Report.Detention?.Status ?? RMCBrigSentenceStatus.Ready;
            var button = Button("rmc-brig-select", () => Send(RMCBrigCellAction.Select, view.Paper), flat: true);
            button.Text = Loc.GetString("rmc-brig-report-entry", ("name", view.Report.Suspect),
                ("status", Loc.GetString($"rmc-brig-status-{status}")));
            button.Selected = view.Paper == state.ViewedReport;
            Left.AddChild(button);
        }

        if (state.Reports.FirstOrDefault(view => view.Paper == state.ViewedReport) is not { } selected)
        {
            Right.AddChild(Text(Loc.GetString("rmc-brig-no-report"), RMCCrtTone.Muted));
            return;
        }

        var report = selected.Report;
        var detention = report.Detention?.Status ?? RMCBrigSentenceStatus.Ready;
        IncidentDetails(Right, report, _prototypes, ComponentFactory, includeSentence: false, forCell: true);
        Right.AddChild(new RMCCrtSeparator { Margin = new Thickness(0, 6) });
        Right.AddChild(Text(Loc.GetString("rmc-brig-total-sentence",
            ("sentence", Sentence(report, _prototypes, ComponentFactory))), RMCCrtTone.Warning));
        Right.AddChild(Heading("rmc-brig-timer"));
        if (detention is not (RMCBrigSentenceStatus.Served or RMCBrigSentenceStatus.Pardoned))
            Right.AddChild(Text(Loc.GetString("rmc-brig-status-value",
                ("status", Loc.GetString($"rmc-brig-status-{detention}"))), RMCCrtTone.Muted));
        if (report.FinalSentence is { Punishment: RMCMarineLawPunishment.Brig } &&
            detention is not (RMCBrigSentenceStatus.Served or RMCBrigSentenceStatus.Pardoned))
            Right.AddChild(Text(Loc.GetString("rmc-brig-remaining"), RMCCrtTone.Muted));
        _timer = Text(string.Empty, RMCCrtTone.Warning);
        _timer.TextFontSize = 18;
        Right.AddChild(_timer);
        _served = Text(string.Empty, RMCCrtTone.Warning);
        Right.AddChild(_served);
        if (report.Detention?.StartedAt is { } started)
            Right.AddChild(Text(Loc.GetString("rmc-brig-started-at", ("time", started.ToString(@"hh\:mm\:ss")))));
        _finish = Text(string.Empty);
        Right.AddChild(_finish);
        if (report.Detention is { PardonedBy: { } officer, PardonedAt: { } pardoned })
            Right.AddChild(Text(Loc.GetString("rmc-brig-pardoned-by",
                ("officer", officer), ("time", pardoned.ToString(@"hh\:mm\:ss")))));
        UpdateTimer();

        var actions = Horizontal();
        if (detention is RMCBrigSentenceStatus.Ready or RMCBrigSentenceStatus.Paused)
        {
            actions.AddChild(Button(detention == RMCBrigSentenceStatus.Ready ? "rmc-brig-start" : "rmc-brig-resume",
                () => Send(RMCBrigCellAction.Start)));
        }
        if (state.ActiveReport == selected.Paper)
        {
            actions.AddChild(Button("rmc-brig-pause", () => Send(RMCBrigCellAction.Pause)));
            actions.AddChild(Button("rmc-brig-end", () => ConfirmAction(RMCBrigCellAction.End, "rmc-brig-end"),
                danger: true));
        }
        if (actions.ChildCount > 0)
            Right.AddChild(actions);
        var management = Horizontal();
        management.AddChild(Button("rmc-brig-reset", () => ConfirmAction(RMCBrigCellAction.Reset, "rmc-brig-reset"),
            danger: true));
        management.AddChild(Button("rmc-brig-eject", () => Send(RMCBrigCellAction.Eject)));
        Right.AddChild(management);
        if (canPardon && detention is not (RMCBrigSentenceStatus.Served or RMCBrigSentenceStatus.Pardoned))
        {
            Right.AddChild(Button("rmc-brig-pardon", () => ConfirmAction(RMCBrigCellAction.Pardon, "rmc-brig-pardon"),
                RMCCrtIcons.Ban, danger: true));
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _timerRefreshAccumulator += args.DeltaSeconds;
        if (_timerRefreshAccumulator < 0.25f)
            return;
        _timerRefreshAccumulator = 0;
        UpdateTimer();
        if (_state == null || CanPardon == null || _timing.CurTime < _nextPermissionRefresh)
            return;
        _nextPermissionRefresh = _timing.CurTime + TimeSpan.FromSeconds(1);
        var canPardon = CanPardon();
        if (_canPardon != canPardon)
            Update(_state, canPardon);
    }

    private void UpdateTimer()
    {
        if (_timer == null || _state?.Reports.FirstOrDefault(view => view.Paper == _state.ViewedReport) is not { } selected)
            return;
        var report = selected.Report;
        var status = report.Detention?.Status ?? RMCBrigSentenceStatus.Ready;
        var served = (report.Detention ?? new RMCBrigSentence()).GetTimeServed(_timing.CurTime,
            _state.ActiveReport == selected.Paper ? _state.StartedAt : null, report.FinalSentence);
        _timer.Text = status is RMCBrigSentenceStatus.Served or RMCBrigSentenceStatus.Pardoned
            ? Loc.GetString($"rmc-brig-status-{status}")
            : report.FinalSentence is { Punishment: RMCMarineLawPunishment.Brig } sentence
                ? RMCBrigSentence.FormatTime(_state.ActiveReport == selected.Paper && _state.FinishAt is { } deadline
                    ? deadline - _timing.CurTime
                    : TimeSpan.FromMinutes(sentence.BrigTime) - served)
                : Loc.GetString("rmc-brig-indefinite");
        if (_served != null)
            _served.Text = Loc.GetString("rmc-brig-time-served",
                ("time", RMCBrigSentence.FormatTime(served, roundUp: false)));
        if (_finish != null)
        {
            var finished = report.Detention?.FinishedAt;
            if (status == RMCBrigSentenceStatus.Pardoned)
                _finish.Text = string.Empty;
            else if (finished != null)
                _finish.Text = Loc.GetString("rmc-brig-finished-at", ("time", finished.Value.ToString(@"hh\:mm\:ss")));
            else if (_state.ActiveReport == selected.Paper && _state.FinishAt is { } finishAt)
                _finish.Text = Loc.GetString("rmc-brig-expected-end",
                    ("time", (_systems.GetEntitySystem<RMCClockSystem>().GetWorldTime() + (finishAt > _timing.CurTime
                        ? finishAt - _timing.CurTime : TimeSpan.Zero)).ToString("HH:mm:ss")));
            else
                _finish.Text = string.Empty;
            _finish.Visible = !string.IsNullOrEmpty(_finish.Text);
        }
    }

    private void Send(RMCBrigCellAction action, NetEntity? paper = null)
    {
        if (_state != null)
            Message?.Invoke(new RMCBrigCellMsg(action, _state.Revision, paper ?? _state.ViewedReport));
    }

    private void ConfirmAction(RMCBrigCellAction action, LocId title)
    {
        if (_state == null)
            return;
        var message = new RMCBrigCellMsg(action, _state.Revision, _state.ViewedReport);
        Confirm(title, () => Message?.Invoke(message));
    }
}
