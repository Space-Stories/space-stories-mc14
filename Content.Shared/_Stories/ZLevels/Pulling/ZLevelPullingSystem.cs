using System.Numerics;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.ZLevels.Pulling;

public sealed partial class ZLevelPullingSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;

    private EntityQuery<ZLevelPullingTransitionComponent> _transitionQuery;
    private EntityQuery<ZPhysicsComponent> _zPhysicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _transitionQuery = GetEntityQuery<ZLevelPullingTransitionComponent>();
        _zPhysicsQuery = GetEntityQuery<ZPhysicsComponent>();

        SubscribeLocalEvent<ActivePullerComponent, ZLevelBeforeMapMoveEvent>(OnPullerMove);
        SubscribeLocalEvent<ZLevelPullingTransitionComponent, ZLevelMapMoveEvent>(OnPulledEntityMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<ZLevelPullingTransitionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextTransition is null)
                continue;

            if (comp.TargetPuller is null)
                continue;

            if (comp.NextTransition < _timing.CurTime)
            {
                FinishTransition(uid, comp);
                continue;
            }

            // Move entity towards target position (saved position of puller at transition start)
            var currentPos = _transform.GetWorldPosition(uid);
            var direction = (comp.TargetPosition - currentPos).Normalized();
            var distance = (comp.TargetPosition - currentPos).Length();
            var moveDistance = comp.TransitionSpeed * frameTime;

            if (moveDistance >= distance)
            {
                _transform.SetWorldPosition(uid, comp.TargetPosition);
            }
            else
            {
                _transform.SetWorldPosition(uid, currentPos + direction * moveDistance);
            }
        }
    }

    private void OnPullerMove(Entity<ActivePullerComponent> ent, ref ZLevelBeforeMapMoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (HasComp<ZLevelTransitioningComponent>(ent.Owner))
            return;

        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling == null)
            return;

        var pulledEntity = puller.Pulling.Value;

        // Get the current position of the puller before transition
        var pullerPos = _transform.GetWorldPosition(ent);

        // Add transition component to the pulled entity
        var transComp = EnsureComp<ZLevelPullingTransitionComponent>(pulledEntity);
        transComp.TargetPuller = ent;
        transComp.StartPosition = _transform.GetWorldPosition(pulledEntity);
        transComp.TargetPosition = pullerPos;  // Save puller's position at the moment of transition
        transComp.TargetZLevel = args.CurrentZLevel;

        var distance = Vector2.Distance(transComp.StartPosition, transComp.TargetPosition);
        var duration = TimeSpan.FromSeconds(distance / transComp.TransitionSpeed);
        transComp.NextTransition = _timing.CurTime + duration;

        Dirty(pulledEntity, transComp);

        if (TryComp<PullableComponent>(pulledEntity, out var pulledComp))
        {
            _pulling.RemovePullJointDuringTransition(pulledEntity, pulledComp);
        }
    }

    private void OnPulledEntityMove(Entity<ZLevelPullingTransitionComponent> ent, ref ZLevelMapMoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        // Check if the pulled entity reached the target z-level
        if (args.CurrentZLevel != ent.Comp.TargetZLevel)
            return;

        TryResumePulling(ent, ent.Comp);
    }

    private void FinishTransition(EntityUid uid, ZLevelPullingTransitionComponent comp)
    {
        comp.NextTransition = null;
        Dirty(uid, comp);

        // If still transitioning and entity didn't move to target level, stop transition
        if (!Exists(uid) || !_transitionQuery.HasComp(uid))
            return;

        if (comp.TargetPuller is { } puller && Exists(puller))
        {
            var pullerMap = Transform(puller).MapUid;
            var pulledMap = Transform(uid).MapUid;
            if (pulledMap != pullerMap && pulledMap != null && pullerMap != null)
            {
                if (_zLevels.TryGetZLevelOffset(pulledMap.Value, pullerMap.Value, out var offset) && offset != 0)
                {
                    _zLevels.TryMove(uid, offset);
                    var pullerPos = _transform.GetWorldPosition(puller);
                    var currentPulledPos = _transform.GetWorldPosition(uid);
                    var diff = currentPulledPos - pullerPos;
                    if (diff.LengthSquared() > 1.0f)
                    {
                        var clampedPos = pullerPos + (diff == Vector2.Zero ? new Vector2(0, 0.5f) : Vector2.Normalize(diff) * 0.8f);
                        _transform.SetWorldPosition(uid, clampedPos);
                    }
                }
            }
        }

        TryResumePulling(uid, comp);
    }

    /// <summary>
    /// Attempts to resume pulling after the transition is complete (either by time or z-level change).
    /// Removes the transition component whether pulling succeeds or not.
    /// </summary>
    private void TryResumePulling(EntityUid uid, ZLevelPullingTransitionComponent comp)
    {
        // Check if puller still exists
        if (!Exists(comp.TargetPuller))
        {
            RemComp<ZLevelPullingTransitionComponent>(uid);
            return;
        }

        // Check if both entities are on the same map
        var pulledXform = Transform(uid);
        var pullerXform = Transform(comp.TargetPuller.Value);
        if (pulledXform.MapID == MapId.Nullspace || pulledXform.MapID != pullerXform.MapID)
        {
            RemComp<ZLevelPullingTransitionComponent>(uid);
            return;
        }

        if (!_actionBlocker.CanInteract(comp.TargetPuller.Value, uid))
        {
            RemComp<ZLevelPullingTransitionComponent>(uid);
            return;
        }

        RemComp<ZLevelPullingTransitionComponent>(uid);
        _pulling.TryStartPull(comp.TargetPuller.Value, uid);
    }
}
