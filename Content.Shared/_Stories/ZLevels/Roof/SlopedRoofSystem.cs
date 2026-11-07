using System.Numerics;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Climbing.Events;
using Content.Shared.Ghost;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.ZLevels.Roof;

public sealed class SlopedRoofSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;

    private EntityQuery<MapGridComponent> _mapGridQuery;
    private readonly Dictionary<EntityUid, TimeSpan> _lastSlide = new();
    private readonly List<EntityUid> _slideCleanup = new();
    private float _checkTimer;

    public override void Initialize()
    {
        base.Initialize();

        _mapGridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<MobStateComponent, MoveEvent>(OnMobMove);
        SubscribeLocalEvent<MobStateComponent, LandEvent>(OnMobLand);
        SubscribeLocalEvent<ItemComponent, LandEvent>(OnItemLand);
        SubscribeLocalEvent<AttemptClimbEvent>(OnAttemptClimb);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        _checkTimer += frameTime;
        if (_checkTimer >= 1.0f)
        {
            _checkTimer = 0f;

            var mobQuery = EntityQueryEnumerator<MobStateComponent, PhysicsComponent, TransformComponent>();
            while (mobQuery.MoveNext(out var uid, out _, out var physics, out var xform))
            {
                TrySlideEntity(uid, physics, xform);
            }
        }

        if (_lastSlide.Count > 50)
        {
            var curTime = _timing.CurTime;
            _slideCleanup.Clear();
            foreach (var (ent, until) in _lastSlide)
            {
                if (curTime > until || TerminatingOrDeleted(ent))
                    _slideCleanup.Add(ent);
            }
            foreach (var ent in _slideCleanup)
            {
                _lastSlide.Remove(ent);
            }
        }
    }

    private void OnMobMove(Entity<MobStateComponent> ent, ref MoveEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.OldPosition.EntityId == args.NewPosition.EntityId &&
            (args.OldPosition.Position - args.NewPosition.Position).LengthSquared() < 0.25f)
        {
            return;
        }

        if (!TryComp<PhysicsComponent>(ent.Owner, out var physics))
            return;

        TrySlideEntity(ent.Owner, physics, args.Component);
    }

    private void OnMobLand(Entity<MobStateComponent> ent, ref LandEvent args)
    {
        if (_net.IsClient)
            return;

        if (TryComp<PhysicsComponent>(ent.Owner, out var physics))
            TrySlideEntity(ent.Owner, physics, Transform(ent.Owner));
    }

    private void OnItemLand(Entity<ItemComponent> ent, ref LandEvent args)
    {
        if (_net.IsClient)
            return;

        if (TryComp<PhysicsComponent>(ent.Owner, out var physics))
            TrySlideEntity(ent.Owner, physics, Transform(ent.Owner));
    }


    private bool IsAirborne(EntityUid uid, PhysicsComponent? physics = null)
    {
        if (Resolve(uid, ref physics, false))
        {
            if (physics.BodyStatus == BodyStatus.InAir)
                return true;
        }

        if (TryComp<ZPhysicsComponent>(uid, out var zPhys))
        {
            if (zPhys.LocalPosition > 0.05f || zPhys.Velocity < -0.1f)
                return true;
        }

        if (HasComp<ThrownItemComponent>(uid))
            return true;

        return false;
    }

    private void TrySlideEntity(EntityUid uid, PhysicsComponent physics, TransformComponent xform)
    {
        if (physics.BodyType == BodyType.Static)
            return;

        if (TerminatingOrDeleted(uid))
            return;

        if (HasComp<GhostComponent>(uid))
            return;

        if (!HasComp<ItemComponent>(uid) && !HasComp<MobStateComponent>(uid))
            return;

        if (IsAirborne(uid, physics))
            return;

        if (_lastSlide.TryGetValue(uid, out var until) && _timing.CurTime < until)
            return;

        if (xform.GridUid is not { } gridUid || !_mapGridQuery.TryComp(gridUid, out var grid))
            return;

        var tilePos = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (!_map.TryGetTileRef(gridUid, grid, tilePos, out var tileRef) || tileRef.Tile.IsEmpty)
            return;

        var tileDef = (ContentTileDefinition) _tileDef[tileRef.Tile.TypeId];
        if (!tileDef.SlopedRoof)
            return;

        Vector2 dirVec;
        float distance;
        if (tileDef.SlopeDirection is { } slopeDir)
        {
            dirVec = slopeDir.ToVec();
            if (dirVec == Vector2.Zero)
                dirVec = new Vector2(0, -1);
            distance = GetDistanceToRoofEdge(gridUid, grid, tilePos, dirVec);
        }
        else
        {
            if (!TryFindClosestHole(gridUid, grid, tilePos, out dirVec, out distance))
            {
                dirVec = new Vector2(0, -1);
                distance = 3f;
            }
        }

        var throwDistance = distance + 1.2f;
        var throwSpeed = Math.Clamp(throwDistance * 2.2f, 3.5f, 8.0f);
        var throwVec = dirVec * throwDistance;

        var slideTime = TimeSpan.FromSeconds(Math.Max(0.8f, (throwDistance / throwSpeed) + 0.4f));
        _lastSlide[uid] = _timing.CurTime + slideTime;

        if (HasComp<MobStateComponent>(uid))
        {
            _stun.TryKnockdown(uid, slideTime, true);
            _popup.PopupEntity(Loc.GetString("stories-sloped-roof-slip"), uid, uid, PopupType.SmallCaution);
        }

        _throwing.TryThrow(uid, throwVec, baseThrowSpeed: throwSpeed, user: null, pushbackRatio: 0f, playSound: false, doSpin: false);
    }

    private float GetDistanceToRoofEdge(EntityUid gridUid, MapGridComponent grid, Vector2i startTile, Vector2 dir)
    {
        var normDir = Vector2.Normalize(dir);
        for (var step = 1; step <= 25; step++)
        {
            var offset = normDir * step;
            var checkTile = startTile + new Vector2i((int)MathF.Round(offset.X), (int)MathF.Round(offset.Y));
            if (IsHole(gridUid, grid, checkTile))
            {
                return offset.Length();
            }
        }

        return 3f;
    }

    private bool TryFindClosestHole(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tilePos,
        out Vector2 direction,
        out float distance)
    {
        for (var r = 1; r <= 5; r++)
        {
            var holeVectors = Vector2.Zero;
            var holeCount = 0;

            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        continue;

                    var checkPos = tilePos + new Vector2i(dx, dy);
                    if (IsHole(gridUid, grid, checkPos))
                    {
                        holeVectors += new Vector2(dx, dy);
                        holeCount++;
                    }
                }
            }

            if (holeCount > 0 && holeVectors != Vector2.Zero)
            {
                direction = Vector2.Normalize(holeVectors);
                distance = r;
                return true;
            }
        }

        direction = new Vector2(0, -1);
        distance = 3f;
        return false;
    }

    private bool IsHole(EntityUid gridUid, MapGridComponent grid, Vector2i tilePos)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tilePos, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        var def = (ContentTileDefinition) _tileDef[tileRef.Tile.TypeId];
        return def.Transparent;
    }

    private void OnAttemptClimb(ref AttemptClimbEvent args)
    {
        var xform = Transform(args.Climbable);
        if (xform.MapUid is not { } mapUid)
            return;

        var worldPos = _transform.GetWorldPosition(args.Climbable);
        if (_zLevels.TryMapUp(mapUid, out var mapAboveUid) &&
            _zLevels.TryFindGridAt(mapAboveUid, worldPos, out var gUid, out var g) &&
            _map.TryGetTileRef(gUid, g, worldPos, out var tileRef) &&
            !tileRef.Tile.IsEmpty &&
            _tileDef[tileRef.Tile.TypeId] is ContentTileDefinition { SlopedRoof: true })
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("stories-sloped-roof-too-sloped"), args.User, args.User, PopupType.SmallCaution);
        }
    }
}
