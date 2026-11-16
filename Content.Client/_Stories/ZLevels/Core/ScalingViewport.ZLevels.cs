// ReSharper disable CheckNamespace

using System.Numerics;
using Content.Client._Stories.ZLevels.Core;
using Robust.Client.Input;
using Robust.Client.Placement;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ITileDefinitionManager _tile = default!;

    private ClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;
    private SharedPhysicsSystem? _physics;
    private SharedTransformSystem? _transformSystem;

    private EntityQuery<TransformComponent>? _xformQuery;
    private EntityQuery<MapComponent>? _mapQuery;
    private EntityQuery<OccluderComponent>? _occluderQuery;

    private IEye? _fallbackEye;
    private bool _cvarHooked;

    // --- Multi-frame visibility cache ---
    private readonly Dictionary<EntityUid, bool> _visibilityCache = new();
    private Vector2i _cachePlayerTile;
    private EntityUid? _cachePlayerMapUid;
    private Angle _cacheEyeRotation;
    private int _visibilityCacheAge;
    private const int VisibilityCacheMaxAge = 15; // ~0.25s cache invalidation

    // --- Cached visible-above level count ---
    private int _cachedLookUpLevels;
    private Vector2i _cachedLookUpPlayerTile;
    private EntityUid? _cachedLookUpMapUid;

    // --- ZEye pool (avoids per-frame GC allocation) ---
    private ZEye? _reusableZEye;

    // --- Cached PlacementOverlay reference ---
    private Overlay? _cachedPlacementOverlay;
    private bool _placementOverlayResolved;

    private readonly List<Vector2> _visibleOpenings = new();

    private static readonly Vector2[] OpeningSampleOffsets =
    [
        Vector2.Zero,
        new Vector2(0.35f, 0.35f),
        new Vector2(-0.35f, 0.35f),
        new Vector2(0.35f, -0.35f),
        new Vector2(-0.35f, -0.35f),
    ];

    /// <summary>
    /// Checks if a lower Z-level is physically visible from the viewport vantage point.
    /// Returns false if the vantage point is in an enclosed room with complete solid floor or all openings
    /// are occluded by occluders, avoiding unnecessary rendering of lower levels.
    /// </summary>
    public bool IsLowerLevelPhysicallyVisible(IClydeViewport viewport, EntityUid activeMapUid, Vector2 viewWorldPos)
    {
        if (_visibilityCache.TryGetValue(activeMapUid, out var cached))
            return cached;

        var res = ComputeIsLowerLevelPhysicallyVisible(viewport, activeMapUid, viewWorldPos);
        _visibilityCache[activeMapUid] = res;
        return res;
    }

    private void InvalidateVisibilityCaches()
    {
        _visibilityCache.Clear();
        _visibleOpenings.Clear();
        _visibilityCacheAge = 0;
        _cachedLookUpMapUid = null;
    }

    private bool HasNonEmptyNeighbor(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (!_mapSystem!.GetTileRef(gridUid, grid, new Vector2i(pos.X + dx, pos.Y + dy)).Tile.IsEmpty)
                    return true;
            }
        }

        return false;
    }

    private bool HasShaftOpeningBelow(EntityUid mapUid, List<Vector2> visibleOpeningsAbove)
    {
        if (_zLevels == null || visibleOpeningsAbove.Count == 0)
            return false;

        foreach (var openingPos in visibleOpeningsAbove)
        {
            if (_zLevels.IsTileTransparentOrEmpty(mapUid, openingPos))
                return true;

            if (_zLevels.IsTileTransparentOrEmpty(mapUid, openingPos + new Vector2(0.5f, 0f)) ||
                _zLevels.IsTileTransparentOrEmpty(mapUid, openingPos + new Vector2(-0.5f, 0f)) ||
                _zLevels.IsTileTransparentOrEmpty(mapUid, openingPos + new Vector2(0f, 0.5f)) ||
                _zLevels.IsTileTransparentOrEmpty(mapUid, openingPos + new Vector2(0f, -0.5f)))
            {
                return true;
            }
        }

        return false;
    }

    private bool ComputeIsLowerLevelPhysicallyVisible(IClydeViewport viewport, EntityUid activeMapUid, Vector2 viewWorldPos)
    {
        _visibleOpenings.Clear();

        if (_xformQuery is null || !_xformQuery.Value.TryComp(activeMapUid, out var xform))
            return true;

        if (_mapSystem is null || _physics is null || _zLevels is null || _occluderQuery is null)
            return true;

        var mapId = xform.MapID;

        var hasMapBelow = _zLevels.TryMapOffset(activeMapUid, -1, out var mapBelowEnt);
        MapId mapBelowId = default;
        if (hasMapBelow && _mapQuery != null && _mapQuery.Value.TryComp(mapBelowEnt.Owner, out var mapBelowComp))
            mapBelowId = mapBelowComp.MapId;

        // Viewport visible world bounds calculated directly from this viewport
        var p0 = viewport.LocalToWorld(Vector2.Zero).Position;
        var p1 = viewport.LocalToWorld(new Vector2(viewport.Size.X, 0)).Position;
        var p2 = viewport.LocalToWorld(new Vector2(0, viewport.Size.Y)).Position;
        var p3 = viewport.LocalToWorld(new Vector2(viewport.Size.X, viewport.Size.Y)).Position;

        var minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        var minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        var maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        var maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

        // Enlarge by 2 tiles so edge tiles and glance-angle openings are never missed
        var box = new Box2(minX, minY, maxX, maxY).Enlarged(2f);
        var checkedAny = false;

        const float maxViewDist = 36f;
        var maxViewDistSq = maxViewDist * maxViewDist;

        var checkOcclusion = _fallbackEye == null || (_fallbackEye.DrawFov && _fallbackEye.DrawLight);

        EntityUid? ignoringEnt = null;
        if (_player.LocalEntity is { } localPlayer &&
            _xformQuery.Value.TryComp(localPlayer, out var plXform) &&
            plXform.MapID == mapId)
        {
            ignoringEnt = localPlayer;
        }

        foreach (var grid in _mapManager.FindGridsIntersecting(mapId, box))
        {
            var gridUid = grid.Owner;
            var gp0 = _mapSystem.TileIndicesFor(gridUid, grid, new MapCoordinates(new Vector2(box.Left, box.Bottom), mapId));
            var gp1 = _mapSystem.TileIndicesFor(gridUid, grid, new MapCoordinates(new Vector2(box.Right, box.Top), mapId));

            var startX = Math.Min(gp0.X, gp1.X);
            var endX = Math.Max(gp0.X, gp1.X);
            var startY = Math.Min(gp0.Y, gp1.Y);
            var endY = Math.Max(gp0.Y, gp1.Y);

            // Iterate over all potentially visible tiles on this grid within viewport bounds
            for (var x = startX; x <= endX; x++)
            {
                for (var y = startY; y <= endY; y++)
                {
                    checkedAny = true;
                    var tile = _mapSystem.GetTileRef(gridUid, grid, new Vector2i(x, y));
                    var isOpen = false;

                    if (tile.Tile.IsEmpty)
                    {
                        if (HasNonEmptyNeighbor(gridUid, grid, new Vector2i(x, y)))
                        {
                            var worldPos = _mapSystem.GridTileToWorld(gridUid, grid, new Vector2i(x, y)).Position;
                            if (hasMapBelow && _zLevels.TryFindGridAt(mapBelowEnt.Owner, worldPos, out var gBelowUid, out var gBelow) &&
                                !_mapSystem.GetTileRef(gBelowUid, gBelow, _mapSystem.WorldToTile(gBelowUid, gBelow, worldPos)).Tile.IsEmpty)
                            {
                                isOpen = true;
                            }
                        }
                    }
                    else
                    {
                        var tileDef = (ContentTileDefinition)_tile[tile.Tile.TypeId];
                        isOpen = tileDef.Transparent;
                    }

                    if (!isOpen)
                        continue;

                    // Tile is open (hole or transparent) and within the screen viewport!
                    var tileCenter = _mapSystem.GridTileToWorld(gridUid, grid, new Vector2i(x, y)).Position;
                    var toTile = tileCenter - viewWorldPos;
                    var distSq = toTile.LengthSquared();

                    if (distSq > maxViewDistSq)
                        continue;

                    var dist = MathF.Sqrt(distSq);
                    var occluded = false;
                    if (checkOcclusion && dist >= 0.2f)
                    {
                        var visible = false;
                        var toCenter = tileCenter - viewWorldPos;
                        var centerDist = toCenter.Length();
                        if (centerDist < 0.2f)
                        {
                            visible = true;
                        }
                        else
                        {
                            var ray = new CollisionRay(viewWorldPos, toCenter / centerDist, (int)CollisionGroup.Opaque);
                            var hit = false;
                            foreach (var _ in _physics.IntersectRayWithPredicate(mapId, ray, ignoringEnt,
                                (uid, ignored) => uid == ignored || !_occluderQuery.Value.TryComp(uid, out var o) || !o.Enabled,
                                centerDist, returnOnFirstHit: true))
                            {
                                hit = true;
                                break;
                            }

                            if (!hit)
                            {
                                visible = true;
                            }
                            else
                            {
                                for (var i = 1; i < OpeningSampleOffsets.Length; i++)
                                {
                                    var cornerPos = tileCenter + OpeningSampleOffsets[i];
                                    var toCorner = cornerPos - viewWorldPos;
                                    var cornerDist = toCorner.Length();
                                    var cornerRay = new CollisionRay(viewWorldPos, toCorner / cornerDist, (int)CollisionGroup.Opaque);
                                    var cornerHit = false;
                                    foreach (var _ in _physics.IntersectRayWithPredicate(mapId, cornerRay, ignoringEnt,
                                        (uid, ignored) => uid == ignored || !_occluderQuery.Value.TryComp(uid, out var o) || !o.Enabled,
                                        cornerDist, returnOnFirstHit: true))
                                    {
                                        cornerHit = true;
                                        break;
                                    }

                                    if (!cornerHit)
                                    {
                                        visible = true;
                                        break;
                                    }
                                }
                            }
                        }

                        occluded = !visible;
                    }

                    if (!occluded)
                    {
                        _visibleOpenings.Add(tileCenter);
                        if (_visibleOpenings.Count >= 15)
                            return true;
                    }
                }
            }
        }

        // Also check if lower map has grids extending into the viewport where upper map has no solid tile (e.g. edge of station, balcony, overhang)
        if (_visibleOpenings.Count < 15 && hasMapBelow)
        {
            foreach (var lowerGrid in _mapManager.FindGridsIntersecting(mapBelowId, box))
            {
                var lowerGridUid = lowerGrid.Owner;
                var lp0 = _mapSystem.TileIndicesFor(lowerGridUid, lowerGrid, new MapCoordinates(new Vector2(box.Left, box.Bottom), mapBelowId));
                var lp1 = _mapSystem.TileIndicesFor(lowerGridUid, lowerGrid, new MapCoordinates(new Vector2(box.Right, box.Top), mapBelowId));
                var lStartX = Math.Min(lp0.X, lp1.X);
                var lEndX = Math.Max(lp0.X, lp1.X);
                var lStartY = Math.Min(lp0.Y, lp1.Y);
                var lEndY = Math.Max(lp0.Y, lp1.Y);

                for (var lx = lStartX; lx <= lEndX; lx++)
                {
                    for (var ly = lStartY; ly <= lEndY; ly++)
                    {
                        var lTile = _mapSystem.GetTileRef(lowerGridUid, lowerGrid, new Vector2i(lx, ly));
                        if (lTile.Tile.IsEmpty)
                            continue;

                        var worldPos = _mapSystem.GridTileToWorld(lowerGridUid, lowerGrid, new Vector2i(lx, ly)).Position;
                        if (_zLevels.IsTileTransparentOrEmpty(activeMapUid, worldPos))
                        {
                            checkedAny = true;
                            var toTile = worldPos - viewWorldPos;
                            var distSq = toTile.LengthSquared();
                            if (distSq > maxViewDistSq)
                                continue;

                            var dist = MathF.Sqrt(distSq);
                            var occluded = false;
                            if (checkOcclusion && dist >= 0.2f)
                            {
                                var visible = false;
                                var ray = new CollisionRay(viewWorldPos, toTile / dist, (int)CollisionGroup.Opaque);
                                var hit = false;
                                foreach (var _ in _physics.IntersectRayWithPredicate(mapId, ray, ignoringEnt,
                                    (uid, ignored) => uid == ignored || !_occluderQuery.Value.TryComp(uid, out var o) || !o.Enabled,
                                    dist, returnOnFirstHit: true))
                                {
                                    hit = true;
                                    break;
                                }

                                if (!hit)
                                {
                                    visible = true;
                                }
                                else
                                {
                                    for (var i = 1; i < OpeningSampleOffsets.Length; i++)
                                    {
                                        var cornerPos = worldPos + OpeningSampleOffsets[i];
                                        var toCorner = cornerPos - viewWorldPos;
                                        var cornerDist = toCorner.Length();
                                        var cornerRay = new CollisionRay(viewWorldPos, toCorner / cornerDist, (int)CollisionGroup.Opaque);
                                        var cornerHit = false;
                                        foreach (var _ in _physics.IntersectRayWithPredicate(mapId, cornerRay, ignoringEnt,
                                            (uid, ignored) => uid == ignored || !_occluderQuery.Value.TryComp(uid, out var o) || !o.Enabled,
                                            cornerDist, returnOnFirstHit: true))
                                        {
                                            cornerHit = true;
                                            break;
                                        }

                                        if (!cornerHit)
                                        {
                                            visible = true;
                                            break;
                                        }
                                    }
                                }

                                occluded = !visible;
                            }

                            if (!occluded)
                            {
                                _visibleOpenings.Add(worldPos);
                                if (_visibleOpenings.Count >= 15)
                                    return true;
                            }
                        }
                    }
                }
            }
        }

        if (!checkedAny)
            return true;

        return _visibleOpenings.Count > 0;
    }

    private void RenderZLevels(IClydeViewport viewport)
    {
        if (_eye is null)
        {
            viewport.Render();
            return;
        }

        _fallbackEye = _eye;

        if (!_cvarHooked)
        {
            _cfg.OnValueChanged(Content.Shared.CCVar.CCVars.ZLevelsRenderingMaxLevels, _ => InvalidateVisibilityCaches());
            _cvarHooked = true;
        }

        _xformQuery ??= _entityManager.GetEntityQuery<TransformComponent>();
        _mapQuery ??= _entityManager.GetEntityQuery<MapComponent>();
        _occluderQuery ??= _entityManager.GetEntityQuery<OccluderComponent>();
        _zLevels ??= _entityManager.System<ClientZLevelsSystem>();
        _mapSystem ??= _entityManager.System<SharedMapSystem>();
        _transformSystem ??= _entityManager.System<SharedTransformSystem>();
        _physics ??= _entityManager.System<SharedPhysicsSystem>();

        var eyePos = _fallbackEye.Position;
        if (!_mapManager.MapExists(eyePos.MapId))
        {
            viewport.Render();
            return;
        }

        var activeMapUid = _mapManager.GetMapEntityId(eyePos.MapId);
        if (!_zLevels.TryGetMapNetwork(activeMapUid, out _))
        {
            viewport.Render();
            return;
        }

        var viewWorldPos = eyePos.Position;
        var viewTile = new Vector2i((int)MathF.Floor(viewWorldPos.X), (int)MathF.Floor(viewWorldPos.Y));

        if (viewTile != _cachePlayerTile ||
            activeMapUid != _cachePlayerMapUid ||
            _fallbackEye.Rotation != _cacheEyeRotation)
        {
            _cachePlayerTile = viewTile;
            _cachePlayerMapUid = activeMapUid;
            _cacheEyeRotation = _fallbackEye.Rotation;
            InvalidateVisibilityCaches();
        }
        else
        {
            _visibilityCacheAge++;
            if (_visibilityCacheAge >= VisibilityCacheMaxAge)
                InvalidateVisibilityCaches();
        }

        var maxLevels = _cfg.GetCVar(Content.Shared.CCVar.CCVars.ZLevelsRenderingMaxLevels);

        // --- Determine visible Z-levels above ---
        var lookUp = 0;
        if (maxLevels > 0 &&
            _player.LocalEntity is { } localPlayer &&
            _entityManager.TryGetComponent<ZLevelViewerComponent>(localPlayer, out var zLevelViewer) &&
            zLevelViewer.LookUp &&
            _xformQuery.Value.TryComp(localPlayer, out var pXform) &&
            pXform.MapID == eyePos.MapId)
        {
            if (_cachedLookUpMapUid == activeMapUid && _cachedLookUpPlayerTile == viewTile)
            {
                lookUp = _cachedLookUpLevels;
            }
            else
            {
                lookUp = _zLevels.GetVisibleZLevelsAbove(localPlayer, activeMapUid);
                lookUp = Math.Min(lookUp, maxLevels);
                _cachedLookUpLevels = lookUp;
                _cachedLookUpPlayerTile = viewTile;
                _cachedLookUpMapUid = activeMapUid;
            }
        }
        else
        {
            _cachedLookUpMapUid = null;
        }

        // --- Determine lowest visible Z-level below based on physical visibility ---
        var lowestDepth = 0;
        var currentMapToCheck = activeMapUid;
        var fovDisabled = _fallbackEye != null && (!_fallbackEye.DrawFov || !_fallbackEye.DrawLight);

        if (maxLevels > 0)
        {
            for (var level = 1; level <= maxLevels; level++)
            {
                if (level == 1)
                {
                    if (!IsLowerLevelPhysicallyVisible(viewport, activeMapUid, viewWorldPos))
                        break;
                }
                else
                {
                    if (!fovDisabled && !HasShaftOpeningBelow(currentMapToCheck, _visibleOpenings))
                        break;
                }

                if (!_zLevels.TryMapOffset(activeMapUid, -level, out var mapUidBelow))
                    break;

                lowestDepth = -level;
                currentMapToCheck = mapUidBelow;
            }
        }

        // Fast path: if lower levels are physically not visible and not peeking up, render single pass
        if (lowestDepth == 0 && lookUp == 0)
        {
            viewport.Eye = _fallbackEye;
            viewport.Render();
            return;
        }

        // Resolve PlacementOverlay (cached after first lookup)
        if (!_placementOverlayResolved)
        {
            foreach (var overlay in _overlayManager.AllOverlays)
            {
                if (overlay.GetType().Name == "PlacementOverlay")
                {
                    _cachedPlacementOverlay = overlay;
                    break;
                }
            }
            _placementOverlayResolved = true;
        }
        var placementOverlay = _cachedPlacementOverlay;

        Vector2 rotationOffsetUnit;
        if (SharedZLevelsSystem.ZLevelOffset != 0f)
        {
            var rotation = -_fallbackEye.Rotation;
            rotationOffsetUnit = rotation.RotateVec(new Vector2(0, SharedZLevelsSystem.ZLevelOffset));
        }
        else
        {
            rotationOffsetUnit = Vector2.Zero;
        }

        try
        {
            // From lowest depth to highest, render each level
            for (var depth = lowestDepth; depth <= lookUp; depth++)
            {
                if (depth == 0)
                {
                    var canDrawPlacement = true;
                    if (_placement.IsActive && _placement.CurrentMode is { } currentMode)
                    {
                        var mouseMapId = _transformSystem?.GetMapId(currentMode.MouseCoords);
                        if (mouseMapId != _fallbackEye!.Position.MapId)
                        {
                            currentMode.AlignPlacementMode(_inputManager.MouseScreenPosition);
                            mouseMapId = _transformSystem?.GetMapId(currentMode.MouseCoords);
                            if (mouseMapId != _fallbackEye!.Position.MapId)
                                canDrawPlacement = false;
                        }
                    }

                    if (placementOverlay != null)
                    {
                        if (canDrawPlacement && !_overlayManager.HasOverlay(placementOverlay.GetType()))
                            _overlayManager.AddOverlay(placementOverlay);
                        else if (!canDrawPlacement && _overlayManager.HasOverlay(placementOverlay.GetType()))
                            _overlayManager.RemoveOverlay(placementOverlay);
                    }

                    var zeye = GetOrUpdateZEye(lowestDepth, 0, lookUp);
                    zeye.Position = _fallbackEye!.Position;
                    zeye.DrawFov = _fallbackEye.DrawFov;
                    zeye.DrawLight = _fallbackEye.DrawLight;
                    zeye.Offset = _fallbackEye.Offset;
                    zeye.Rotation = _fallbackEye.Rotation;
                    zeye.Scale = _fallbackEye.Scale;
                    viewport.Eye = zeye;
                }
                else
                {
                    if (placementOverlay != null && _overlayManager.HasOverlay(placementOverlay.GetType()))
                        _overlayManager.RemoveOverlay(placementOverlay);

                    if (!_zLevels.TryMapOffset(activeMapUid, depth, out var targetMap))
                        continue;

                    if (!_mapQuery.Value.TryComp(targetMap, out var mapComp))
                        continue;

                    var zeye = GetOrUpdateZEye(lowestDepth, depth, lookUp);
                    zeye.Position = new MapCoordinates(_fallbackEye!.Position.Position, mapComp.MapId);
                    // For lower levels (depth < 0), do not draw FOV mask from player eye as player is on a different map.
                    // This allows lighting and tiles on the lower level to be visible through openings without being blacked out.
                    zeye.DrawFov = _fallbackEye.DrawFov && depth >= 0;
                    zeye.DrawLight = _fallbackEye.DrawLight;
                    zeye.Offset = _fallbackEye.Offset + rotationOffsetUnit * depth;
                    zeye.Rotation = _fallbackEye.Rotation;
                    zeye.Scale = _fallbackEye.Scale;
                    viewport.Eye = zeye;
                }

                try
                {
                    viewport.ClearColor = depth == lowestDepth ? Color.Black : null;
                    viewport.Render();
                }
                catch
                {
                    // Suppress overlays that cannot render across different map coordinates
                }
            }
        }
        finally
        {
            if (placementOverlay != null && !_overlayManager.HasOverlay(placementOverlay.GetType()))
                _overlayManager.AddOverlay(placementOverlay);

            Eye = _fallbackEye;
            viewport.Eye = Eye;
        }
    }

    private ZEye GetOrUpdateZEye(int lowest, int depth, int high)
    {
        if (_reusableZEye == null)
        {
            _reusableZEye = new ZEye(lowest, depth, high);
            return _reusableZEye;
        }

        _reusableZEye.LowestDepth = lowest;
        _reusableZEye.Depth = depth;
        _reusableZEye.HighestDepth = high;
        return _reusableZEye;
    }

    public sealed partial class ZEye(int lowest, int depth, int high) : Robust.Shared.Graphics.Eye
    {
        public int LowestDepth = lowest;
        public int Depth = depth;
        public int HighestDepth = high;
    }
}
