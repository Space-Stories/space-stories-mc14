using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Xenonids.UI;
using Content.Client.Message;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Evolution;

[UsedImplicitly]
public sealed class XenoEvolutionBui : BoundUserInterface
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly XenoEvolutionSystem _evolution;
    private readonly MobStateSystem _mobState;
    private readonly SpriteSystem _sprite;

    [ViewVariables]
    private XenoEvolutionWindow? _window;

    private XenoEvolutionPathWindow? _pathWindow;

    private static readonly Color QueuedHighlightColor = Color.FromHex("#4CAF50");

    private readonly Dictionary<EntProtoId, XenoChoiceControl> _evolutionControls = new();
    private readonly Dictionary<EntProtoId, XenoChoiceControl> _raffleControls = new();
    private readonly Dictionary<EntProtoId, XenoChoiceControl> _strainControls = new();
    private readonly Dictionary<XenoChoiceControl, Action> _buttonActions = new();

    private string? _cachedProtoId;
    private bool _cachedMarinesLanded;
    private List<(EntProtoId Target, string Name, List<List<EntProtoId>> Paths)> _cachedRaffleTargets = new();

    public XenoEvolutionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _evolution = EntMan.System<XenoEvolutionSystem>();
        _mobState = EntMan.System<MobStateSystem>();
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _pathWindow?.Close();
            _pathWindow = null;
        }
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<XenoEvolutionWindow>();
        _window.OvipositorNeededLabel.Visible = false;

        if (EntMan.TryGetComponent(Owner, out XenoEvolutionComponent? xeno))
        {
            foreach (var strain in xeno.Strains)
            {
                AddStrain(strain);
            }
        }

        _window.StrainsLabel.Visible = _window.StrainsContainer.ChildCount > 0;
        Refresh();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        Refresh();
    }

    private XenoChoiceControl GetOrCreateChoiceControl(
        Dictionary<EntProtoId, XenoChoiceControl> pool,
        EntProtoId id,
        Control container)
    {
        if (!pool.TryGetValue(id, out var control))
        {
            control = new XenoChoiceControl();
            control.Texture.MinSize = new Vector2(48, 32);
            control.Texture.Stretch = TextureRect.StretchMode.KeepCentered;

            var captured = control;
            control.Button.OnPressed += _ =>
            {
                if (_buttonActions.TryGetValue(captured, out var action))
                    action();
            };

            pool[id] = control;
            container.AddChild(control);
        }

        return control;
    }

    private void AddEvolution(EntProtoId evolutionId)
    {
        if (_window == null || !_prototype.TryIndex(evolutionId, out var evolution))
            return;

        var control = GetOrCreateChoiceControl(_evolutionControls, evolutionId, _window.EvolutionsContainer);
        control.Set(evolution.Name, _sprite.Frame0(evolution));
        control.Button.ModulateSelfOverride = null;
        _buttonActions[control] = () =>
        {
            SendPredictedMessage(new XenoEvolveBuiMsg(evolutionId));
            Close();
        };

        control.Visible = true;
        control.Button.Disabled = false;
    }

    // Stories-XenoEvolutionQueue-Start
    private void AddRaffleChoice(
        EntProtoId targetId,
        int candidateCount,
        bool queued,
        Dictionary<EntProtoId, XenoChoiceControl> pool,
        Control container,
        List<EntProtoId>? path = null,
        List<List<EntProtoId>>? allPaths = null)
    {
        if (_window is not { IsOpen: true })
            return;

        if (!_prototype.TryIndex(targetId, out var target))
            return;

        var control = GetOrCreateChoiceControl(pool, targetId, container);
        control.Set($"{target.Name} ({candidateCount})", _sprite.Frame0(target));
        control.Button.ModulateSelfOverride = queued ? QueuedHighlightColor : null;

        if (queued)
        {
            _buttonActions[control] = () => SendPredictedMessage(new XenoLeaveRaffleBuiMsg());
        }
        else if (allPaths is { Count: > 1 })
        {
            _buttonActions[control] = () => OpenPathSelection(targetId, allPaths);
        }
        else
        {
            var chosenPath = path ?? allPaths?.FirstOrDefault();
            _buttonActions[control] = () => SendPredictedMessage(new XenoJoinRaffleBuiMsg(targetId, chosenPath));
        }

        control.Visible = true;
        control.Button.Disabled = false;
    }

    private void OpenPathSelection(EntProtoId targetId, List<List<EntProtoId>> paths)
    {
        _pathWindow?.Close();
        _pathWindow = new XenoEvolutionPathWindow();

        foreach (var path in paths)
        {
            var button = new Button
            {
                HorizontalExpand = true,
                StyleClasses = { "ButtonSquare" },
                ToolTip = FormatPath(path),
            };

            var iconsRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalAlignment = Control.HAlignment.Center,
                SeparationOverride = 8,
                Margin = new Thickness(4, 2),
            };

            foreach (var step in path)
            {
                if (!_prototype.TryIndex(step, out var stepProto))
                    continue;

                iconsRow.AddChild(new TextureRect
                {
                    Texture = _sprite.Frame0(stepProto),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    MinSize = new Vector2(48, 48),
                });
            }

            button.AddChild(iconsRow);

            var chosen = path;
            button.OnPressed += _ =>
            {
                SendPredictedMessage(new XenoJoinRaffleBuiMsg(targetId, chosen));
                _pathWindow?.Close();
                _pathWindow = null;
            };

            _pathWindow.PathsContainer.AddChild(button);
        }

        _pathWindow.OpenCentered();
    }

    private string FormatPath(List<EntProtoId> path)
    {
        var names = new List<string>(path.Count);
        foreach (var step in path)
        {
            names.Add(_prototype.TryIndex(step, out var proto) ? proto.Name : step.Id);
        }

        return string.Join(" → ", names);
    }
    // Stories-XenoEvolutionQueue-End

    private void AddStrain(EntProtoId strainId)
    {
        if (_window is not { IsOpen: true })
            return;

        if (!_prototype.TryIndex(strainId, out var strain))
            return;

        if (!_strainControls.TryGetValue(strainId, out var control))
        {
            control = new XenoChoiceControl();

            var name = strain.Name;
            string? description = null;

            if (strain.TryGetComponent(out XenoStrainComponent? strainComp))
            {
                name = $"{Loc.GetString(strainComp.Name)} {name}";
                description = strainComp.Description;
            }

            control.Set(name, _sprite.Frame0(strain));
            control.Button.Disabled = false;

            control.Button.OnPressed += _ =>
            {
                var confirmWindow = new XenoStrainConfirmWindow();
                confirmWindow.SetInfo(name, _sprite.Frame0(strain), description);

                confirmWindow.OnConfirm += () =>
                {
                    SendPredictedMessage(new XenoStrainBuiMsg(strainId));
                    confirmWindow.Close();
                    Close();
                };

                confirmWindow.OpenCentered();
            };

            _strainControls[strainId] = control;
            _window.StrainsContainer.AddChild(control);
        }

        control.Visible = true;
        control.Button.Disabled = false;
    }

    public void Refresh()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out XenoEvolutionComponent? xeno))
            return;

        _window.PointsLabel.Visible = xeno.Max > FixedPoint2.Zero;

        // Stories-XenoEvolutionQueue-Start
        foreach (var control in _evolutionControls.Values)
            control.Visible = false;

        foreach (var control in _raffleControls.Values)
            control.Visible = false;

        var state = State as XenoEvolveBuiState;
        EntMan.TryGetComponent(Owner, out XenoRaffleCandidateComponent? myCandidate);

        var hasQueenAlive = HiveHasLivingQueen();
        foreach (var evolutionId in xeno.EvolvesToWithoutPoints)
        {
            if (hasQueenAlive &&
                _prototype.TryIndex(evolutionId, out var proto) &&
                proto.TryGetComponent(out XenoEvolutionGranterComponent? _, _compFactory))
            {
                continue;
            }

            AddEvolutionOrRaffle(evolutionId, xeno, state, myCandidate, true);
        }

        foreach (var evolutionId in xeno.EvolvesTo)
            AddEvolutionOrRaffle(evolutionId, xeno, state, myCandidate);

        if (!xeno.MarinesLanded)
        {
            foreach (var evolutionId in xeno.EarlyEvolvesTo)
                AddEvolutionOrRaffle(evolutionId, xeno, state, myCandidate);
        }

        if (EntMan.TryGetComponent(Owner, out MetaDataComponent? meta) &&
            meta.EntityPrototype is { } ownerProto)
        {
            if (_cachedProtoId != ownerProto.ID || _cachedMarinesLanded != xeno.MarinesLanded)
            {
                _cachedProtoId = ownerProto.ID;
                _cachedMarinesLanded = xeno.MarinesLanded;
                _cachedRaffleTargets = BuildSortedRaffleTargets(ownerProto.ID, xeno.MarinesLanded);
            }

            foreach (var entry in _cachedRaffleTargets)
            {
                var queued = myCandidate != null && myCandidate.Target == entry.Target;
                var count = GetCandidateCount(state, entry.Target);

                AddRaffleChoice(
                    entry.Target,
                    count,
                    queued,
                    _raffleControls,
                    _window.RaffleContainer,
                    allPaths: entry.Paths);
            }
        }

        var showRaffleTab = _raffleControls.Values.Any(c => c.Visible);
        _window.TabContainer.SetTabVisible(1, showRaffleTab);
        if (!showRaffleTab && _window.TabContainer.CurrentTab == 1)
            _window.TabContainer.CurrentTab = 0;
        // Stories-XenoEvolutionQueue-End

        _window.Separator.Visible = _window.EvolutionsContainer.Children.Any(child => child.Visible) &&
                                    _window.StrainsContainer.Children.Any(child => child.Visible);

        var lackingOvipositor = state is { LackingOvipositor: true };
        var points = xeno.Points;

        _window.PointsLabel.Text = Loc.GetString("rmc-xeno-ui-evolution-points",
            ("points", (int)Math.Floor(points.Double())),
            ("maxPoints", xeno.Max));

        if (lackingOvipositor && xeno.Max > FixedPoint2.Zero)
        {
            if (!_window.OvipositorNeededLabel.Visible)
            {
                _window.OvipositorNeededLabel.SetMarkupPermissive(Loc.GetString("rmc-xeno-ui-ovi-needed-label"));
                _window.OvipositorNeededLabel.Visible = true;
            }
        }
        else if (_window.OvipositorNeededLabel.Visible)
        {
            _window.OvipositorNeededLabel.Visible = false;
        }
    }

    // Stories-XenoEvolutionQueue-Start
    private List<(EntProtoId Target, string Name, List<List<EntProtoId>> Paths)> BuildSortedRaffleTargets(
        string ownerProtoId,
        bool marinesLanded)
    {
        var allPaths = _evolution.GetEvolutionPaths(ownerProtoId, marinesLanded);
        var byTarget = new Dictionary<EntProtoId, List<List<EntProtoId>>>();
        foreach (var path in allPaths)
        {
            if (path.Count == 0)
                continue;

            var target = path[^1];
            if (!byTarget.TryGetValue(target, out var list))
            {
                list = new List<List<EntProtoId>>();
                byTarget[target] = list;
            }

            list.Add(path);
        }

        return byTarget
            .Select(kvp =>
            {
                _prototype.TryIndex(kvp.Key, out var proto);
                var tier = proto != null && proto.TryGetComponent(out XenoComponent? xenoComp, _compFactory)
                    ? xenoComp.Tier
                    : 0;
                var name = proto?.Name ?? kvp.Key.Id;
                return (Target: kvp.Key, Name: name, Tier: tier, Paths: kvp.Value);
            })
            .OrderBy(x => x.Tier)
            .ThenBy(x => x.Name)
            .Select(x => (x.Target, x.Name, x.Paths))
            .ToList();
    }

    private void AddEvolutionOrRaffle(
        EntProtoId evolutionId,
        XenoEvolutionComponent xeno,
        XenoEvolveBuiState? state,
        XenoRaffleCandidateComponent? myCandidate,
        bool withoutPoints = false)
    {
        if (_window == null)
            return;

        var queued = myCandidate != null && myCandidate.Target == evolutionId;
        if (queued)
        {
            AddRaffleChoice(evolutionId, GetCandidateCount(state, evolutionId), true, _evolutionControls, _window.EvolutionsContainer);
            return;
        }

        var lacksPoints = !withoutPoints && xeno.Points < xeno.Max;
        if (lacksPoints || (state != null && state.RaffleGatedTargets.Contains(evolutionId.Id)))
        {
            AddRaffleChoice(
                evolutionId,
                GetCandidateCount(state, evolutionId),
                false,
                _evolutionControls,
                _window.EvolutionsContainer,
                new List<EntProtoId> { evolutionId });
            return;
        }

        AddEvolution(evolutionId);
    }
    // Stories-XenoEvolutionQueue-End

    private static int GetCandidateCount(XenoEvolveBuiState? state, EntProtoId targetId)
    {
        if (state == null || string.IsNullOrEmpty(targetId.Id))
            return 0;

        return state.RaffleCandidates.GetValueOrDefault(targetId.Id);
    }

    private bool HiveHasLivingQueen()
    {
        if (!EntMan.TryGetComponent(Owner, out HiveMemberComponent? member) ||
            member.Hive is not { } hive ||
            !EntMan.TryGetComponent(hive, out HiveComponent? hiveComp) ||
            hiveComp.CurrentQueen is not { } queen)
        {
            return false;
        }

        return !_mobState.IsDead(queen);
    }
}
