using Content.Shared._RMC14.Tether;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Tether;

public sealed class StoriesTetherSystem : VirtualController
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _toRelease = new();

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<StoriesTetheredComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StoriesTetheredComponent, StoriesTetherResistAlertEvent>(OnResistAlert);
        SubscribeLocalEvent<StoriesTetheredComponent, StoriesTetherResistDoAfterEvent>(OnResistDoAfter);
    }

    public void Tether(EntityUid target, EntityUid origin, int range, string visualState, bool resistible = true)
    {
        if (target == origin)
            return;

        Release(target);

        var comp = EnsureComp<StoriesTetheredComponent>(target);
        comp.Origin = origin;
        comp.Range = range;
        comp.Resistible = resistible;
        Dirty(target, comp);

        _alerts.ShowAlert(target, comp.Alert);

        var visual = EnsureComp<RMCTetherComponent>(origin);
        visual.TetherOrigin = target;
        visual.TetherState = visualState;
        Dirty(origin, visual);
    }

    public void Release(EntityUid target)
    {
        if (TryComp(target, out StoriesTetheredComponent? comp) && comp.LifeStage <= ComponentLifeStage.Running)
            RemComp(target, comp);
    }

    public bool IsTetheredTo(EntityUid target, EntityUid origin)
    {
        return TryComp(target, out StoriesTetheredComponent? comp) &&
               comp.LifeStage <= ComponentLifeStage.Running &&
               comp.Origin == origin;
    }

    private void OnShutdown(Entity<StoriesTetheredComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);

        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Origin is not { } origin || TerminatingOrDeleted(origin))
            return;

        if (TryComp(origin, out RMCTetherComponent? visual) && visual.TetherOrigin == ent.Owner)
            RemComp<RMCTetherComponent>(origin);

        var ev = new StoriesTetherReleasedEvent(ent.Owner, origin);
        RaiseLocalEvent(origin, ref ev);
    }

    private void OnResistAlert(Entity<StoriesTetheredComponent> ent, ref StoriesTetherResistAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!ent.Comp.Resistible)
        {
            _popup.PopupClient(Loc.GetString("stories-tether-resist-impossible"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.ResistTime, new StoriesTetherResistDoAfterEvent(GetNetEntity(ent.Comp.Origin)), ent)
        {
            BreakOnMove = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("stories-tether-resist-start", ("seconds", (int) ent.Comp.ResistTime.TotalSeconds)), ent, ent, PopupType.SmallCaution);
    }

    private void OnResistDoAfter(Entity<StoriesTetheredComponent> ent, ref StoriesTetherResistDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (GetEntity(args.Origin) != ent.Comp.Origin)
            return;

        _popup.PopupClient(Loc.GetString("stories-tether-resist-success"), ent, ent, PopupType.Medium);
        Release(ent);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        _toRelease.Clear();

        var query = EntityQueryEnumerator<StoriesTetheredComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tether, out var physics, out var xform))
        {
            if (prediction && !physics.Predict)
                continue;

            if (tether.Origin is not { } origin ||
                TerminatingOrDeleted(origin) ||
                !TryComp(origin, out TransformComponent? originXform) ||
                originXform.MapID != xform.MapID)
            {
                _toRelease.Add(uid);
                continue;
            }

            if (_container.IsEntityInContainer(uid))
                continue;

            var sameParent = xform.ParentUid == originXform.ParentUid;
            var position = sameParent ? xform.LocalPosition : _transform.GetWorldPosition(xform);
            var originPosition = sameParent ? originXform.LocalPosition : _transform.GetWorldPosition(originXform);
            var rotation = sameParent ? _transform.GetWorldRotation(xform.ParentUid) : Angle.Zero;

            var delta = position - originPosition;
            var bound = (float) tether.Range;
            var breakBound = bound + tether.BreakDistance;

            if (MathF.Abs(delta.X) > breakBound || MathF.Abs(delta.Y) > breakBound)
            {
                _toRelease.Add(uid);
                continue;
            }

            var velocity = (-rotation).RotateVec(physics.LinearVelocity);
            var clampedVelocity = velocity;
            var clampedDelta = delta;

            ClampAxis(ref clampedDelta.X, ref clampedVelocity.X, bound);
            ClampAxis(ref clampedDelta.Y, ref clampedVelocity.Y, bound);

            if (clampedDelta != delta)
            {
                var newPosition = originPosition + clampedDelta;
                if (sameParent)
                    _transform.SetLocalPosition(uid, newPosition, xform);
                else
                    _transform.SetWorldPosition((uid, xform), newPosition);
            }

            if (clampedVelocity != velocity)
                _physics.SetLinearVelocity(uid, rotation.RotateVec(clampedVelocity), body: physics);
        }

        if (_net.IsClient)
            return;

        foreach (var uid in _toRelease)
        {
            Release(uid);
        }
    }

    private static void ClampAxis(ref float delta, ref float velocity, float bound)
    {
        if (delta >= bound)
        {
            delta = bound;
            if (velocity > 0)
                velocity = 0;
        }
        else if (delta <= -bound)
        {
            delta = -bound;
            if (velocity < 0)
                velocity = 0;
        }
    }
}
