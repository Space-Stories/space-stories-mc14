using System.Numerics;
using Content.Server.Decals;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._Stories.Blood;
using Content.Shared._Stories.SCCVars;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Decals;
using Content.Shared.GameTicking;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Blood;

public sealed class BloodTracksSystem : EntitySystem
{
    private static readonly ProtoId<DecalPrototype> BareLeftDecal = "STDecalBloodFootprintBareLeft";
    private static readonly ProtoId<DecalPrototype> BareRightDecal = "STDecalBloodFootprintBareRight";
    private static readonly ProtoId<DecalPrototype> ShoesDecal = "STDecalBloodFootprintShoes";

    private static readonly ProtoId<DecalPrototype>[] DragDecals =
    [
        "STDecalBloodDrag1",
        "STDecalBloodDrag2",
        "STDecalBloodDrag3",
        "STDecalBloodDrag4",
        "STDecalBloodDrag5",
    ];

    private static readonly Angle TrackRotationOffset = Angle.FromDegrees(180);
    private static readonly Vector2 DecalCenterOffset = new(0.5f, 0.5f);

    private static readonly Dictionary<EntProtoId, Color> SplatterSpawners = new()
    {
        ["RMCDecalSpawnerBloodSplatters"] = Color.FromHex("#980002"),
        ["RMCDecalSpawnerXenoSplatters"] = Color.FromHex("#BED700"),
        ["RMCDecalSpawnerAcidBloodSplash"] = Color.FromHex("#BED700"),
    };

    [Dependency] private readonly BloodStainSystem _bloodStain = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCReagentSystem _reagent = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PuddleComponent> _puddleQuery;

    // High performance O(1) tracking structures
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), WetSplatter> _wetSplatters = new();
    private readonly Queue<(TimeSpan DriesAt, EntityUid Grid, Vector2i Tile)> _wetSplatterQueue = new();

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), byte> _trackCounts = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), byte> _splatterCounts = new();
    private readonly Queue<(TimeSpan ExpireTime, EntityUid Grid, Vector2i Tile, uint DecalId, bool IsSplatter)> _decalQueue = new();

    private readonly HashSet<Entity<BloodyComponent>> _nearbyBloody = new();

    private TimeSpan _splatterDryTime;
    private int _wetSplatterCharges;
    private float _splatterStainRange;
    private int _maxTracksPerTile;
    private int _maxSplattersPerTile;

    private record struct WetSplatter(Color Color, TimeSpan DriesAt, int Charges);

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _puddleQuery = GetEntityQuery<PuddleComponent>();

        SubscribeLocalEvent<BloodyComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<RandomDecalSpawnerComponent, ComponentStartup>(OnDecalSpawnerStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        Subs.CVar(_config, SCCVars.BloodSplatterDryTime, v => _splatterDryTime = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, SCCVars.BloodWetSplatterCharges, v => _wetSplatterCharges = v, true);
        Subs.CVar(_config, SCCVars.BloodSplatterStainRange, v => _splatterStainRange = v, true);
        Subs.CVar(_config, SCCVars.BloodMaxTracksPerTile, v => _maxTracksPerTile = v, true);
        Subs.CVar(_config, SCCVars.BloodMaxFloorSplattersPerTile, v => _maxSplattersPerTile = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // O(1) amortized cleanup of expired decals
        while (_decalQueue.TryPeek(out var item) && item.ExpireTime <= now)
        {
            _decalQueue.Dequeue();
            if (TerminatingOrDeleted(item.Grid))
                continue;

            _decals.RemoveDecal(item.Grid, item.DecalId);

            var countDict = item.IsSplatter ? _splatterCounts : _trackCounts;
            var key = (item.Grid, item.Tile);
            if (countDict.TryGetValue(key, out var count))
            {
                if (count <= 1)
                    countDict.Remove(key);
                else
                    countDict[key] = (byte)(count - 1);
            }
        }

        // O(1) amortized cleanup of expired wet splatters
        while (_wetSplatterQueue.TryPeek(out var item) && item.DriesAt <= now)
        {
            _wetSplatterQueue.Dequeue();
            var key = (item.Grid, item.Tile);
            if (_wetSplatters.TryGetValue(key, out var splatter) && splatter.DriesAt <= now)
            {
                _wetSplatters.Remove(key);
            }
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _wetSplatters.Clear();
        _wetSplatterQueue.Clear();
        _trackCounts.Clear();
        _splatterCounts.Clear();
        _decalQueue.Clear();
    }

    private void OnDecalSpawnerStartup(Entity<RandomDecalSpawnerComponent> ent, ref ComponentStartup args)
    {
        if (MetaData(ent).EntityPrototype?.ID is not { } protoId ||
            !SplatterSpawners.TryGetValue(protoId, out var color))
        {
            return;
        }

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid ||
            !_map.IsInitialized(xform.MapUid) ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return;
        }

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var radius = (int)MathF.Ceiling(ent.Comp.Radius);
        var driesAt = _timing.CurTime + _splatterDryTime;

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var tile = center + new Vector2i(x, y);
                var key = (gridUid, tile);
                _wetSplatters[key] = new WetSplatter(color, driesAt, _wetSplatterCharges);
                _wetSplatterQueue.Enqueue((driesAt, gridUid, tile));
            }
        }

        _nearbyBloody.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(ent, xform), _splatterStainRange, _nearbyBloody);
        foreach (var bloody in _nearbyBloody)
        {
            _bloodStain.StainWearer(bloody.AsNullable(), BloodStainParts.Body, color);
        }
    }

    public bool TryAddSplatter(ProtoId<DecalPrototype> decal, EntityUid gridUid, MapGridComponent grid, Vector2 center, Color color, Angle rotation)
    {
        var tile = _map.TileIndicesFor(gridUid, grid, new EntityCoordinates(gridUid, center));
        var key = (gridUid, tile);

        if (_splatterCounts.TryGetValue(key, out var count) && count >= _maxSplattersPerTile)
            return false;

        var position = center - DecalCenterOffset;
        if (!_decals.TryAddDecal(decal, new EntityCoordinates(gridUid, position), out var id, color, rotation, cleanable: true))
            return false;

        _splatterCounts[key] = (byte)(count + 1);
        var driesAt = _timing.CurTime + _splatterDryTime;
        _decalQueue.Enqueue((driesAt, gridUid, tile, id, true));

        _wetSplatters[key] = new WetSplatter(color, driesAt, _wetSplatterCharges);
        _wetSplatterQueue.Enqueue((driesAt, gridUid, tile));

        return true;
    }

    private void OnMove(Entity<BloodyComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.LeavesTracks)
            return;

        var xform = args.Component;
        if (xform.GridUid is not { } gridUid ||
            xform.ParentUid != gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            ent.Comp.TrackGrid = null;
            return;
        }

        var position = xform.LocalPosition;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        var isDragged = IsDragged(ent);
        var canStep = CanLeaveFootprints(ent);

        // Fast path: if not leaving marks and staying on the same tile, exit immediately
        if (!isDragged && !canStep && tile == ent.Comp.LastTile && ent.Comp.TrackGrid == gridUid)
            return;

        if (ent.Comp.TrackGrid != gridUid)
        {
            ent.Comp.TrackGrid = gridUid;
            ent.Comp.LastTile = tile;
            ent.Comp.LastTrackPosition = position;
            TryPickUpBlood(ent, gridUid, grid, tile, position);
            return;
        }

        // Stepping onto a new tile: check for blood
        if (tile != ent.Comp.LastTile)
        {
            ent.Comp.LastTile = tile;
            TryPickUpBlood(ent, gridUid, grid, tile, position);
        }

        var delta = position - ent.Comp.LastTrackPosition;
        var distSq = delta.LengthSquared();
        if (distSq > ent.Comp.MaxTrackJump * ent.Comp.MaxTrackJump)
        {
            ent.Comp.LastTrackPosition = position;
            return;
        }

        if (isDragged)
        {
            if (distSq >= ent.Comp.DragDistance * ent.Comp.DragDistance)
                TryLeaveDragTrail(ent, gridUid, grid, position, delta);
        }
        else if (canStep)
        {
            if (distSq >= ent.Comp.StepDistance * ent.Comp.StepDistance)
                TryLeaveFootprint(ent, gridUid, grid, position, delta);
        }
        else
        {
            ent.Comp.LastTrackPosition = position;
        }
    }

    private void TryPickUpBlood(Entity<BloodyComponent> ent, EntityUid gridUid, MapGridComponent grid, Vector2i tile, Vector2 position)
    {
        if (!TryGetPuddleBlood(ent, gridUid, grid, tile, out var volume, out var color))
        {
            if (!TryGetWetSplatter(gridUid, tile, out color))
                return;

            volume = ent.Comp.WetSplatterVolume;
        }

        var steps = Math.Clamp((int)MathF.Ceiling(volume / ent.Comp.VolumePerStep), 1, ent.Comp.MaxSteps);
        if (_standing.IsDown(ent) || !_mobState.IsAlive(ent))
        {
            ent.Comp.DragSteps = Math.Max(ent.Comp.DragSteps, ent.Comp.MaxDragSteps);
            ent.Comp.TrackColor = color;
            _bloodStain.StainWearer(ent.AsNullable(), BloodStainParts.Body, color);
            return;
        }

        if (ent.Comp.Steps <= 0)
            ent.Comp.LastTrackPosition = position;

        ent.Comp.Steps = Math.Max(ent.Comp.Steps, steps);
        ent.Comp.TrackColor = color;
        _bloodStain.StainWearer(ent.AsNullable(), BloodStainParts.Feet, color);
    }

    private bool TryGetPuddleBlood(Entity<BloodyComponent> ent, EntityUid gridUid, MapGridComponent grid, Vector2i tile, out float volume, out Color color)
    {
        volume = 0f;
        color = default;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!_puddleQuery.TryComp(uid, out var puddle) ||
                !_solution.ResolveSolution(uid.Value, puddle.SolutionName, ref puddle.Solution, out var solution))
            {
                continue;
            }

            var puddleVolume = 0f;
            var largest = 0f;
            foreach (var reagent in solution.Contents)
            {
                if (!ent.Comp.BloodReagents.Contains(reagent.Reagent.Prototype))
                    continue;

                var quantity = reagent.Quantity.Float();
                puddleVolume += quantity;
                if (quantity <= largest || !_reagent.TryIndex(reagent.Reagent, out var proto))
                    continue;

                largest = quantity;
                color = proto.SubstanceColor;
            }

            if (puddleVolume < ent.Comp.MinPuddleVolume)
                continue;

            // Non-destructive read: does not mutate puddle solution to avoid ECS fluid recalculation spam
            volume = puddleVolume;
            return true;
        }

        return false;
    }

    private bool TryGetWetSplatter(EntityUid gridUid, Vector2i tile, out Color color)
    {
        color = default;
        var key = (gridUid, tile);
        if (!_wetSplatters.TryGetValue(key, out var splatter))
            return false;

        if (splatter.DriesAt <= _timing.CurTime || splatter.Charges <= 0)
        {
            _wetSplatters.Remove(key);
            return false;
        }

        if (splatter.Charges <= 1)
            _wetSplatters.Remove(key);
        else
            _wetSplatters[key] = splatter with { Charges = splatter.Charges - 1 };

        color = splatter.Color;
        return true;
    }

    private bool IsDragged(EntityUid uid)
    {
        return TryComp<PullableComponent>(uid, out var pullable) &&
               pullable.BeingPulled &&
               (_standing.IsDown(uid) || !_mobState.IsAlive(uid));
    }

    private bool CanLeaveFootprints(Entity<BloodyComponent> ent)
    {
        return ent.Comp.Steps > 0 && !_standing.IsDown(ent) && _mobState.IsAlive(ent);
    }

    private void TryLeaveDragTrail(Entity<BloodyComponent> ent, EntityUid gridUid, MapGridComponent grid, Vector2 position, Vector2 delta)
    {
        var bleeding = TryComp<BloodstreamComponent>(ent, out var bloodstream) && bloodstream.BleedAmount > 0;
        if (!bleeding && ent.Comp.DragSteps <= 0)
        {
            ent.Comp.LastTrackPosition = position;
            return;
        }

        Color color;
        if (bleeding && _bloodStain.TryGetBloodColor(ent, out var bloodColor))
        {
            color = bloodColor.WithAlpha(ent.Comp.BleedingDragAlpha);
        }
        else
        {
            color = ent.Comp.TrackColor.WithAlpha(GetTrackAlpha(ent.Comp, ent.Comp.DragSteps, ent.Comp.MaxDragSteps));
            ent.Comp.DragSteps--;
        }

        var center = ent.Comp.LastTrackPosition + delta / 2;
        AddTrackDecal(_random.Pick(DragDecals), gridUid, grid, center, color, delta.ToWorldAngle());
        ent.Comp.LastTrackPosition = position;
    }

    private void TryLeaveFootprint(Entity<BloodyComponent> ent, EntityUid gridUid, MapGridComponent grid, Vector2 position, Vector2 delta)
    {
        var direction = Vector2.Normalize(delta);
        var side = new Vector2(-direction.Y, direction.X) * ent.Comp.StepSideOffset;
        if (ent.Comp.RightFoot)
            side = -side;

        var color = ent.Comp.TrackColor.WithAlpha(GetTrackAlpha(ent.Comp, ent.Comp.Steps, ent.Comp.MaxSteps));
        ProtoId<DecalPrototype> decal;
        if (_inventory.TryGetSlotEntity(ent, BloodStainSystem.ShoesSlot, out _))
            decal = ShoesDecal;
        else
            decal = ent.Comp.RightFoot ? BareRightDecal : BareLeftDecal;

        AddTrackDecal(decal, gridUid, grid, position + side, color, delta.ToWorldAngle());

        ent.Comp.RightFoot = !ent.Comp.RightFoot;
        ent.Comp.Steps--;
        ent.Comp.LastTrackPosition = position;
    }

    private void AddTrackDecal(ProtoId<DecalPrototype> decal, EntityUid gridUid, MapGridComponent grid, Vector2 center, Color color, Angle rotation)
    {
        if (_maxTracksPerTile <= 0)
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, new EntityCoordinates(gridUid, center));
        var key = (gridUid, tile);

        if (_trackCounts.TryGetValue(key, out var count) && count >= _maxTracksPerTile)
            return;

        var position = center - DecalCenterOffset;
        if (!_decals.TryAddDecal(decal, new EntityCoordinates(gridUid, position), out var id, color, rotation + TrackRotationOffset, cleanable: true))
            return;

        _trackCounts[key] = (byte)(count + 1);
        var expireTime = _timing.CurTime + _splatterDryTime;
        _decalQueue.Enqueue((expireTime, gridUid, tile, id, false));
    }

    private static float GetTrackAlpha(BloodyComponent comp, int remaining, int max)
    {
        var fraction = max <= 0 ? 0f : Math.Clamp((float)remaining / max, 0f, 1f);
        return comp.MinTrackAlpha + (comp.MaxTrackAlpha - comp.MinTrackAlpha) * fraction;
    }
}
