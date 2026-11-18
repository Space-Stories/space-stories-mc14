using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Doors;

public sealed class TurnstilePushSystem : VirtualController
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedBroadphaseSystem _broadphase = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedTurnstileSystem _turnstile = default!;

    private readonly HashSet<EntityUid> _entities = new();
    private readonly List<EntityUid> _finishedPushes = new();

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<TurnstilePushedComponent, PreventCollideEvent>(OnPushedPreventCollide);
        SubscribeLocalEvent<TurnstilePushedComponent, UpdateCanMoveEvent>(OnPushedUpdateCanMove);
        SubscribeLocalEvent<TurnstilePushedComponent, TileFrictionEvent>(OnPushedTileFriction);
        SubscribeLocalEvent<TurnstilePushComponent, TurnstilePassedEvent>(OnTurnstilePassed);
        SubscribeLocalEvent<StoriesDoorButtonPressedEvent>(OnDoorButtonPressed);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        _finishedPushes.Clear();
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<TurnstilePushedComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var physics))
        {
            if (TerminatingOrDeleted(comp.Turnstile))
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                _finishedPushes.Add(uid);
                continue;
            }

            var currentPos = _transform.GetWorldPosition(uid);
            var diff = comp.TargetWorldPosition - currentPos;
            var distance = diff.Length();

            if (distance <= 0.05f || curTime >= comp.EndTime)
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                if (distance <= 0.25f)
                    _transform.SetWorldPosition(uid, comp.TargetWorldPosition);
                _finishedPushes.Add(uid);
                continue;
            }

            var speed = MathF.Min(comp.Speed, distance / MathF.Max(frameTime, 0.001f));
            _physics.SetLinearVelocity(uid, (diff / distance) * speed, body: physics);
            _physics.WakeBody(uid, body: physics);
        }

        foreach (var uid in _finishedPushes)
        {
            RemComp<TurnstilePushedComponent>(uid);
            _blocker.UpdateCanMove(uid);
        }
    }

    private void OnPushedPreventCollide(Entity<TurnstilePushedComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OtherEntity == ent.Comp.Turnstile && args.OurFixture.Hard && args.OtherFixture.Hard)
            args.Cancelled = true;
    }

    private void OnPushedUpdateCanMove(Entity<TurnstilePushedComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnPushedTileFriction(Entity<TurnstilePushedComponent> ent, ref TileFrictionEvent args)
    {
        args.Modifier = 0f;
    }

    private void OnTurnstilePassed(Entity<TurnstilePushComponent> ent, ref TurnstilePassedEvent args)
    {
        if (HasComp<TurnstilePushedComponent>(args.Other) || !HasComp<MobStateComponent>(args.Other))
            return;

        if (!TryComp(ent, out TurnstileComponent? turnstile))
            return;

        var xform = Transform(ent);
        PushEntityToExit((ent.Owner, ent.Comp, turnstile, xform), args.Other);
    }

    private void OnDoorButtonPressed(ref StoriesDoorButtonPressedEvent args)
    {
        var buttonTransform = Transform(args.Button);
        var query = EntityQueryEnumerator<TurnstilePushComponent, TurnstileComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var pushTurnstile, out var turnstile, out var xform, out var metaData))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (buttonTransform.MapID != xform.MapID)
                continue;

            var id = pushTurnstile.Id ?? metaData.EntityName;
            if (args.ButtonId != id)
                continue;

            PushEntitiesOnTile((uid, pushTurnstile, turnstile, xform), args.User);
        }
    }

    private void PushEntitiesOnTile(Entity<TurnstilePushComponent, TurnstileComponent, TransformComponent> ent, EntityUid user)
    {
        var (uid, pushTurnstile, turnstile, xform) = ent;
        if (pushTurnstile.PushSpeed <= 0f || pushTurnstile.PushDistance <= 0f)
            return;

        var (turnstilePos, rot) = _transform.GetWorldPositionRotation(xform);
        var forward = rot.ToWorldVec();
        var barPos = turnstile.LocalCenter != Vector2.Zero
            ? turnstilePos + rot.RotateVec(turnstile.LocalCenter)
            : turnstilePos;

        _entities.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, 0.75f, _entities);

        MapGridComponent? grid = null;
        var hasGrid = xform.GridUid is { } gridUid && TryComp(gridUid, out grid);
        var turnstileTile = hasGrid ? _map.TileIndicesFor(xform.GridUid!.Value, grid!, xform.Coordinates) : Vector2i.Zero;

        EntityUid? closestTarget = null;
        var closestDistSq = float.MaxValue;

        foreach (var target in _entities)
        {
            if (target == uid || TerminatingOrDeleted(target))
                continue;

            var targetXform = Transform(target);
            if (targetXform.Anchored || !HasComp<MobStateComponent>(target))
                continue;

            if (!TryComp<PhysicsComponent>(target, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            var targetPos = _transform.GetWorldPosition(targetXform);
            var onSameTile = hasGrid
                ? targetXform.GridUid == xform.GridUid &&
                  _map.TileIndicesFor(xform.GridUid!.Value, grid!, targetXform.Coordinates) == turnstileTile
                : (targetPos - turnstilePos).Length() <= 0.5f;

            if (!onSameTile)
                continue;

            var distSq = (targetPos - barPos).LengthSquared();
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestTarget = target;
            }
        }

        if (closestTarget != null)
            PushEntityToExit(ent, closestTarget.Value, user);
    }

    private void PushEntityToExit(Entity<TurnstilePushComponent, TurnstileComponent, TransformComponent> ent, EntityUid target, EntityUid? soundUser = null)
    {
        var (uid, pushTurnstile, turnstile, _) = ent;
        if (pushTurnstile.PushSpeed <= 0f || pushTurnstile.PushDistance <= 0f)
            return;

        if (!TryComp<PhysicsComponent>(target, out var physics) || physics.BodyType == BodyType.Static)
            return;

        var targetWorldPos = GetExitTargetWorldPosition(ent);
        var currentWorldPos = _transform.GetWorldPosition(target);
        var distance = (targetWorldPos - currentWorldPos).Length();
        if (distance <= 0.05f)
            return;

        var duration = TimeSpan.FromSeconds(distance / pushTurnstile.PushSpeed + 0.15f);

        _turnstile.AddCollideException((uid, turnstile), target);
        if (soundUser != null)
            _audio.PlayPredicted(turnstile.TurnSound, uid, soundUser);

        var pushed = EnsureComp<TurnstilePushedComponent>(target);
        pushed.Turnstile = uid;
        pushed.TargetWorldPosition = targetWorldPos;
        pushed.Speed = pushTurnstile.PushSpeed;
        pushed.EndTime = _timing.CurTime + duration;
        Dirty(target, pushed);

        _blocker.UpdateCanMove(target);
        _broadphase.RegenerateContacts(target, physics);
        _physics.WakeBody(target, body: physics);
    }

    private Vector2 GetExitTargetWorldPosition(Entity<TurnstilePushComponent, TurnstileComponent, TransformComponent> ent)
    {
        var (_, pushTurnstile, _, xform) = ent;
        var (turnstilePos, rot) = _transform.GetWorldPositionRotation(xform);
        var forward = rot.ToWorldVec();

        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var gridRot = _transform.GetWorldRotation(gridUid);
            var localForward = (-gridRot).RotateVec(forward);
            var step = new Vector2i((int) MathF.Round(localForward.X), (int) MathF.Round(localForward.Y));
            var tileSteps = (int) MathF.Round(pushTurnstile.PushDistance);
            if (step != Vector2i.Zero && tileSteps > 0)
            {
                var exitTile = tile + step * tileSteps;
                var localCenter = _map.GridTileToLocal(gridUid, grid, exitTile);
                return _transform.ToMapCoordinates(localCenter).Position;
            }
        }

        return turnstilePos + forward * pushTurnstile.PushDistance;
    }
}
