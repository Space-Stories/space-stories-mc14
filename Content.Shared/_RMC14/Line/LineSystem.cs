using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared._Stories.ZLevels.Ladder;
using Content.Shared.Beam.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Doors.Components;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Line;

public sealed class LineSystem : EntitySystem
{
    [Dependency] private readonly SharedDirectionalAttackBlockSystem _directionalBlock = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!; // Stories-ZLevels

    private static readonly ProtoId<TagPrototype> StructureTag = "Structure";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> WindowFrameTag = "WindowFrame";
    private static readonly float MaxBeamDistance = 500;

    private EntityQuery<BarricadeComponent> _barricadeQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<MapGridComponent> _mapGridQuery;

    public override void Initialize()
    {
        _barricadeQuery = GetEntityQuery<BarricadeComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _mapGridQuery = GetEntityQuery<MapGridComponent>();
    }

    public List<LineTile> DrawLine(EntityCoordinates start, EntityCoordinates end, TimeSpan delayPer, float? range, out EntityUid? blocker, bool hitBlocker = false, bool thick = false, bool ignoreBarricades = false, EntityUid? user = null)
    {
        blocker = null;
        start = _mapSystem.AlignToGrid(start);
        end = _mapSystem.AlignToGrid(end);
        var tiles = new List<LineTile>();

        var startMapCoords = _transform.ToMapCoordinates(start);
        var endMapCoords = _transform.ToMapCoordinates(end);

        if (startMapCoords.MapId != endMapCoords.MapId && !_zLevels.AreInSameNetwork(startMapCoords.MapId, endMapCoords.MapId))
            return tiles;

        var diff = endMapCoords.Position - startMapCoords.Position;
        var distance = diff.Length();
        if (distance < 0.001f)
            return tiles;

        if (range != null)
            distance = Math.Min(range.Value, distance);

        var dir = diff / distance;
        var time = _timing.CurTime;
        var startGridId = _transform.GetGrid(start.EntityId);
        var endGridId = _transform.GetGrid(end.EntityId);
        var isCrossMap = startMapCoords.MapId != endMapCoords.MapId;

        var aimUp = user != null &&
                    ((TryComp<ZLevelViewerComponent>(user, out var viewer) && viewer.LookUp) ||
                     (TryComp<ZPhysicsComponent>(user, out var uZ) && uZ.LocalPosition > 0.2f));

        var lastCoords = start;
        var hasTransitioned = false;
        var activeMapId = startMapCoords.MapId;

        for (var i = 0; i < distance; i++)
        {
            var worldPos = startMapCoords.Position + dir * (i + 1);

            var currentMapId = activeMapId;
            if (isCrossMap)
            {
                if (hasTransitioned)
                {
                    currentMapId = endMapCoords.MapId;
                }
                else
                {
                    var startMapUid = _mapManager.GetMapEntityId(startMapCoords.MapId);
                    var endMapUid = _mapManager.GetMapEntityId(endMapCoords.MapId);
                    if (_zLevels.TryGetZLevelOffset(startMapUid, endMapUid, out var zOff))
                    {
                        if (zOff < 0 && _zLevels.IsTileTransparentOrEmpty(startMapUid, worldPos))
                        {
                            hasTransitioned = true;
                            currentMapId = endMapCoords.MapId;
                            activeMapId = currentMapId;
                        }
                        else if (zOff > 0 && !_zLevels.HasOpaqueAbove(worldPos, startMapUid))
                        {
                            hasTransitioned = true;
                            currentMapId = endMapCoords.MapId;
                            activeMapId = currentMapId;
                        }
                    }
                }
            }
            else if (aimUp)
            {
                if (hasTransitioned)
                {
                    currentMapId = activeMapId;
                }
                else
                {
                    var startMapUid = _mapManager.GetMapEntityId(startMapCoords.MapId);
                    if (!_zLevels.HasOpaqueAbove(worldPos, startMapUid) && _zLevels.TryMapUp(startMapUid, out var mapAbove))
                    {
                        hasTransitioned = true;
                        currentMapId = _transform.GetMapId(mapAbove.Owner);
                        activeMapId = currentMapId;
                    }
                }
            }
            else
            {
                if (hasTransitioned)
                {
                    currentMapId = activeMapId;
                }
                else
                {
                    var startMapUid = _mapManager.GetMapEntityId(startMapCoords.MapId);
                    if (_zLevels.IsTileTransparentOrEmpty(startMapUid, worldPos))
                    {
                        if (_zLevels.TryMapDown(startMapUid, out var mapBelow))
                        {
                            hasTransitioned = true;
                            currentMapId = _transform.GetMapId(mapBelow.Owner);
                            activeMapId = currentMapId;
                        }
                    }
                }
            }

            EntityUid? currentGridUid = null;
            if (_mapManager.TryFindGridAt(currentMapId, worldPos, out var foundGridUid, out _))
            {
                currentGridUid = foundGridUid;
            }
            else
            {
                currentGridUid = _mapManager.GetMapEntityId(currentMapId);
            }

            var center = _transform.ToCoordinates(currentGridUid.Value, new MapCoordinates(worldPos, currentMapId)).SnapToGrid(EntityManager, _mapManager);
            if (center == lastCoords)
                continue;

            List<EntityCoordinates> coords = new(9);
            coords.Add(center);
            if (thick && i > 1)
            {
                for (var xo = -1; xo < 2; xo++)
                {
                    for (var yo = -1; yo < 2; yo++)
                    {
                        if (xo == 0 && yo == 0)
                            continue;

                        var point = _transform.ToCoordinates(currentGridUid ?? _mapManager.GetMapEntityId(currentMapId), new MapCoordinates(worldPos + new Vector2(xo, yo), currentMapId)).SnapToGrid(EntityManager, _mapManager);
                        coords.Add(point);
                    }
                }
            }

            var centerBlocked = false;
            for (var j = 0; j < coords.Count; j++)
            {
                var entityCoords = coords[j];
                var targetGrid = _transform.GetGrid(entityCoords.EntityId);
                var targetGridComp = targetGrid == null ? null : _mapGridQuery.CompOrNull(targetGrid.Value);
                Entity<MapGridComponent>? checkGrid = targetGridComp == null ? null : new Entity<MapGridComponent>(targetGrid!.Value, targetGridComp);

                var blocked = IsTileBlocked(checkGrid, lastCoords, entityCoords, hitBlocker, out blocker, out var hitBlockerOverride, ignoreBarricades);
                hitBlocker = hitBlockerOverride;

                if (j == 0 && blocked && !hitBlocker)
                {
                    centerBlocked = true;
                    break;
                }

                var mapCoords = _transform.ToMapCoordinates(entityCoords);

                var isDuplicate = false;
                foreach (var existing in tiles)
                {
                    if (existing.Coordinates.Position == mapCoords.Position)
                    {
                        isDuplicate = true;
                        break;
                    }
                }
                if (!isDuplicate)
                {
                    var delay = Vector2.Distance(mapCoords.Position, startMapCoords.Position) - 1;
                    tiles.Add(new LineTile(mapCoords, time + delayPer * delay));
                }

                if (blocked && j == 0)
                {
                    centerBlocked = true;
                    break;
                }
            }

            if (centerBlocked)
                break;

            lastCoords = center;
        }

        return tiles;
    }

    private bool IsTileBlocked(Entity<MapGridComponent>? grid, EntityCoordinates previousCoords, EntityCoordinates coords, bool hitBlocker, [NotNullWhen(true)] out EntityUid? blocker, out bool hitBlockerOverride, bool ignoreBarricades = false)
    {
        blocker = default;
        hitBlockerOverride = hitBlocker;
        if (grid == null)
            return false;

        // Check if the next tile is blocked by more than one wall/structure to prevent the line from passing through diagonally.
        var prevWorld = _transform.ToMapCoordinates(previousCoords).Position;
        var nextWorld = _transform.ToMapCoordinates(coords).Position;
        var prevMap = Transform(previousCoords.EntityId).MapID;
        var nextMap = Transform(coords.EntityId).MapID;
        var direction = nextWorld - prevWorld;
        if (direction != Vector2.Zero && prevMap == nextMap)
        {
            var ray = new CollisionRay(prevWorld, direction.Normalized(), (int)CollisionGroup.FullTileMask);
            var intersect = _physics.IntersectRayWithPredicate(nextMap, ray, direction.Length(), e => !Transform(e).Anchored, false);
            var results = intersect.Select(r => r.HitEntity).ToHashSet();
            var blockCount = 0;
            foreach (var entity in results)
            {
                if (!_tag.HasAnyTag(entity, StructureTag, WallTag))
                    continue;

                blockCount++;

                if (blockCount < 2)
                    continue;

                blocker = entity;
                return true;
            }
        }

        var indices = _mapSystem.TileIndicesFor(grid.Value, grid, coords);
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(grid.Value, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (_barricadeQuery.HasComp(uid))
            {
                if (ignoreBarricades)
                    continue;

                if (_doorQuery.TryComp(uid, out var door) && door.State != DoorState.Closed)
                    continue;

                if (_directionalBlock.IsBehindTarget(uid.Value, coords.EntityId, previousCoords))
                {
                    blocker = uid.Value;
                    return true;
                }
                if (_directionalBlock.IsFacingTarget(uid.Value, coords.EntityId, previousCoords))
                {
                    blocker = uid.Value;
                    hitBlockerOverride = false;
                    return true;
                }
            }
            else if (_doorQuery.TryComp(uid, out var door))
            {
                if (door.State != DoorState.Closed && door.State != DoorState.Denying && door.State != DoorState.Welded)
                    continue;

                blocker = uid.Value;
                return true;
            }
            else if (_tag.HasAnyTag(uid.Value, WallTag))
            {
                blocker = uid.Value;
                return true;
            }
            else if (_tag.HasTag(uid.Value, StructureTag))
            {
                if (ignoreBarricades && _tag.HasTag(uid.Value, WindowFrameTag))
                    continue;

                if (HasComp<ZLevelHighGroundComponent>(uid) ||
                    HasComp<ZLevelLadderComponent>(uid) ||
                    HasComp<ClimbableComponent>(uid) ||
                    !HasComp<PhysicsComponent>(uid))
                {
                    continue;
                }

                if (TryComp<FixturesComponent>(uid, out var fixtures))
                {
                    var blocks = false;
                    foreach (var fixture in fixtures.Fixtures.Values)
                    {
                        if (fixture.Hard && (fixture.CollisionLayer & (int)(CollisionGroup.Impassable | CollisionGroup.HighImpassable)) != 0)
                        {
                            blocks = true;
                            break;
                        }
                    }
                    if (!blocks)
                        continue;
                }

                blocker = uid.Value;
                return true;
            }
        }

        return false;
    }

    //RMC Beam code
    //Purely visual, to have a line update on move etc
    //Delete the previous line this returned and make a new one

    public bool TryCreateLine(EntityUid source, EntityUid target, string proto, out List<EntityUid> lines)
    {
        lines = new List<EntityUid>();

        //Can't run on client
        if (_net.IsClient)
            return false;

        if (Deleted(source) || Deleted(target))
            return false;

        if (_transform.GetMapId(source) != _transform.GetMapId(target))
            return false;

        var sourceMapPos = _transform.GetMapCoordinates(source);
        var targetMapPos = _transform.GetMapCoordinates(target);

        var calculatedDistance = targetMapPos.Position - sourceMapPos.Position;
        var sourceAngle = calculatedDistance.ToWorldAngle();

        if (sourceMapPos.MapId != targetMapPos.MapId)
            return false;

        var beamStartPos = sourceMapPos.Offset(calculatedDistance.Normalized());

        //Don't divide by zero or make a mega beam
        if (calculatedDistance.Length() == 0 || calculatedDistance.Length() > MaxBeamDistance)
            return false;

        var distanceCorrection = calculatedDistance - calculatedDistance.Normalized();

        var beam = Spawn(proto, beamStartPos);
        lines.Add(beam);

        var distanceLength = distanceCorrection.Length();

        var beamVisualizerEvent = new BeamVisualizerEvent(GetNetEntity(beam), distanceLength, sourceAngle, shader: "shaded");
        RaiseNetworkEvent(beamVisualizerEvent);

        for (var i = 0; i < distanceLength - 1; i++)
        {
            beamStartPos = beamStartPos.Offset(calculatedDistance.Normalized());
            var newEnt = Spawn(proto, beamStartPos);
            lines.Add(newEnt);

            var ev = new BeamVisualizerEvent(GetNetEntity(newEnt), distanceLength, sourceAngle, shader: "shaded");
            RaiseNetworkEvent(ev);
        }

        return true;
    }


    public void DeleteBeam(List<EntityUid> beam)
    {
        if (_net.IsClient)
            return;

        foreach (var ent in beam)
        {
            QueueDel(ent);
        }

        beam.Clear();
    }
}
