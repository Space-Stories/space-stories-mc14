using System.Numerics;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared._Stories.ZLevels.Light;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client._Stories.ZLevels.Light;

/// <summary>
/// Manages cross-Z-level dynamic lighting.
/// When a light source on one Z-level can reach an opening (hole or transparent tile) to an adjacent Z-level,
/// a proxy light is placed on the adjacent level at the same world position so it naturally illuminates that level
/// and casts 2D shadows from its walls/occluders.
/// </summary>
public sealed partial class ClientZLevelsLightSystem : SharedZLevelsLightSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<PointLightComponent> _lightQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ZLevelViewerComponent> _viewerQuery;
    private EntityQuery<ZMapComponent> _zMapQuery;
    private EntityQuery<OccluderComponent> _occluderQuery;

    private readonly HashSet<Entity<PointLightComponent>> _nearbyLights = new();
    private readonly List<Vector2> _openingsScratch = new();
    private readonly Dictionary<EntityUid, List<EntityUid>> _proxiesBySource = new();
    private readonly Queue<EntityUid> _proxyPool = new();
    private readonly HashSet<(EntityUid Source, int TargetDepth)> _visitedProxies = new();
    private readonly List<EntityUid> _toRemoveSources = new();

    private float _updateTimer;
    private const float UpdateInterval = 0.20f;
    private const float MaxPlayerRadius = 18f;
    private const float AttenuationFactor = 0.85f;

    public override void Initialize()
    {
        base.Initialize();
        _mapQuery = GetEntityQuery<MapComponent>();
        _lightQuery = GetEntityQuery<PointLightComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _viewerQuery = GetEntityQuery<ZLevelViewerComponent>();
        _zMapQuery = GetEntityQuery<ZMapComponent>();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
        SubscribeLocalEvent<PointLightComponent, ComponentShutdown>(OnLightShutdown);
    }

    private void OnLightShutdown(Entity<PointLightComponent> ent, ref ComponentShutdown args)
    {
        RemoveProxies(ent.Owner);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ClearAllProxies();
    }

    private void ClearAllProxies()
    {
        foreach (var proxies in _proxiesBySource.Values)
        {
            foreach (var proxy in proxies)
            {
                if (Exists(proxy))
                    QueueDel(proxy);
            }
        }

        while (_proxyPool.TryDequeue(out var pooled))
        {
            if (Exists(pooled))
                QueueDel(pooled);
        }

        _proxiesBySource.Clear();
        _visitedProxies.Clear();
        _nearbyLights.Clear();
        _openingsScratch.Clear();
        _toRemoveSources.Clear();
    }

    private void ReleaseProxy(EntityUid proxy)
    {
        if (!Exists(proxy) || TerminatingOrDeleted(proxy))
            return;

        if (_lightQuery.TryComp(proxy, out var pLight))
            _pointLight.SetEnabled(proxy, false, pLight);

        _proxyPool.Enqueue(proxy);
    }

    private void RemoveProxies(EntityUid source)
    {
        if (_proxiesBySource.Remove(source, out var proxies))
        {
            foreach (var proxy in proxies)
            {
                ReleaseProxy(proxy);
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;
        _updateTimer = 0f;

        if (_player.LocalEntity is not { } player ||
            !_viewerQuery.TryComp(player, out _) ||
            !_xformQuery.TryComp(player, out var playerXform) ||
            playerXform.MapUid is not { } activeMapUid ||
            !_zMapQuery.TryComp(activeMapUid, out var activeZMap))
        {
            ClearAllProxies();
            return;
        }

        var playerWorldPos = _transform.GetWorldPosition(playerXform);
        _visitedProxies.Clear();

        // Propagate lighting between active map and map below
        if (activeZMap.MapBelow is { } mapBelow &&
            _zMapQuery.TryComp(mapBelow, out var zMapBelow) &&
            _mapQuery.TryComp(activeMapUid, out var activeMapComp) &&
            _mapQuery.TryComp(mapBelow, out var mapBelowComp))
        {
            ProcessZPair(
                activeMapUid,
                activeMapComp.MapId,
                activeZMap.Depth,
                mapBelow,
                mapBelowComp.MapId,
                zMapBelow.Depth,
                playerWorldPos);
        }

        // Propagate lighting between map above and active map
        if (activeZMap.MapAbove is { } mapAbove &&
            _zMapQuery.TryComp(mapAbove, out var zMapAbove) &&
            _mapQuery.TryComp(mapAbove, out var mapAboveComp) &&
            _mapQuery.TryComp(activeMapUid, out var activeMapCompAbove))
        {
            ProcessZPair(
                mapAbove,
                mapAboveComp.MapId,
                zMapAbove.Depth,
                activeMapUid,
                activeMapCompAbove.MapId,
                activeZMap.Depth,
                playerWorldPos);
        }

        // Clean up proxies that are no longer active
        _toRemoveSources.Clear();
        foreach (var (source, proxies) in _proxiesBySource)
        {
            for (var i = proxies.Count - 1; i >= 0; i--)
            {
                var proxy = proxies[i];
                if (TryComp<ZLevelLightProxyComponent>(proxy, out var proxyComp))
                {
                    if (!_visitedProxies.Contains((source, proxyComp.TargetDepth)))
                    {
                        ReleaseProxy(proxy);
                        proxies.RemoveAt(i);
                    }
                }
            }

            if (proxies.Count == 0)
                _toRemoveSources.Add(source);
        }

        foreach (var source in _toRemoveSources)
        {
            _proxiesBySource.Remove(source);
        }
    }

    private void ProcessZPair(
        EntityUid upperMapUid,
        MapId upperMapId,
        int upperDepth,
        EntityUid lowerMapUid,
        MapId lowerMapId,
        int lowerDepth,
        Vector2 playerWorldPos)
    {
        _openingsScratch.Clear();
        CollectOpenings(upperMapUid, upperMapId, lowerMapUid, playerWorldPos, MaxPlayerRadius, _openingsScratch);

        if (_openingsScratch.Count == 0)
        {
            // No openings separating the two levels near the player; light cannot cross
            return;
        }

        // Process lights on upper map shining down to lower map
        PropagateLights(
            upperMapUid,
            upperMapId,
            upperDepth,
            lowerMapId,
            lowerDepth,
            playerWorldPos,
            _openingsScratch);

        // Process lights on lower map shining up to upper map
        PropagateLights(
            lowerMapUid,
            lowerMapId,
            lowerDepth,
            upperMapId,
            upperDepth,
            playerWorldPos,
            _openingsScratch);
    }

    /// <summary>
    /// Collects world positions of openings (empty tiles with neighbors or transparent tiles) on the upper map.
    /// </summary>
    private bool CollectOpenings(EntityUid mapUid, MapId mapId, EntityUid lowerMapUid, Vector2 center, float radius, List<Vector2> openings)
    {
        var box = Box2.CenteredAround(center, new Vector2(radius * 2f, radius * 2f));
        var hasGrid = false;

        foreach (var grid in _mapManager.FindGridsIntersecting(mapId, box))
        {
            hasGrid = true;
            var gridUid = grid.Owner;
            var p0 = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(new Vector2(box.Left, box.Bottom), mapId));
            var p1 = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(new Vector2(box.Right, box.Bottom), mapId));
            var p2 = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(new Vector2(box.Left, box.Top), mapId));
            var p3 = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(new Vector2(box.Right, box.Top), mapId));

            var startX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
            var endX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
            var startY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
            var endY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

            for (var x = startX; x <= endX; x++)
            {
                for (var y = startY; y <= endY; y++)
                {
                    var tile = _map.GetTileRef(gridUid, grid, new Vector2i(x, y));
                    var isOpen = false;

                    if (tile.Tile.IsEmpty)
                    {
                        if (HasNonEmptyNeighbor(gridUid, grid, new Vector2i(x, y)))
                        {
                            var tileWorldPos = _map.GridTileToWorld(gridUid, grid, new Vector2i(x, y)).Position;
                            if (_zLevels.TryFindGridAt(lowerMapUid, tileWorldPos, out var gBelowUid, out var gBelow) &&
                                !_map.GetTileRef(gBelowUid, gBelow, _map.WorldToTile(gBelowUid, gBelow, tileWorldPos)).Tile.IsEmpty)
                            {
                                isOpen = true;
                            }
                        }
                    }
                    else
                    {
                        var tileDef = (ContentTileDefinition)_tileDef[tile.Tile.TypeId];
                        isOpen = tileDef.Transparent;
                    }

                    if (isOpen)
                    {
                        var tilePos = _map.GridTileToWorld(gridUid, grid, new Vector2i(x, y)).Position;
                        openings.Add(tilePos);
                    }
                }
            }
        }

        return hasGrid;
    }

    private bool HasNonEmptyNeighbor(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (!_map.GetTileRef(gridUid, grid, new Vector2i(pos.X + dx, pos.Y + dy)).Tile.IsEmpty)
                    return true;
            }
        }

        return false;
    }

    private void PropagateLights(
        EntityUid sourceMapUid,
        MapId sourceMapId,
        int sourceDepth,
        MapId targetMapId,
        int targetDepth,
        Vector2 playerWorldPos,
        List<Vector2> openings)
    {
        if (openings.Count == 0)
            return;

        var mapCoords = new MapCoordinates(playerWorldPos, sourceMapId);
        _nearbyLights.Clear();
        _lookup.GetEntitiesInRange(mapCoords, MaxPlayerRadius, _nearbyLights);

        foreach (var lightEnt in _nearbyLights)
        {
            var lightUid = lightEnt.Owner;
            if (HasComp<ZLevelLightProxyComponent>(lightUid))
                continue;

            var lightComp = lightEnt.Comp;
            if (!lightComp.Enabled || lightComp.Radius <= 0.1f)
                continue;

            if (!_xformQuery.TryComp(lightUid, out var lightXform))
                continue;

            var lightPos = _transform.GetWorldPosition(lightXform);
            var lightRadiusSq = lightComp.Radius * lightComp.Radius;

            // Find ONLY the closest opening within the light's radius
            var bestDistSq = float.MaxValue;
            var bestOpening = default(Vector2);
            var foundOpening = false;

            foreach (var opening in openings)
            {
                var distSq = Vector2.DistanceSquared(opening, lightPos);
                if (distSq <= lightRadiusSq && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestOpening = opening;
                    foundOpening = true;
                }
            }

            if (!foundOpening)
                continue;

            var distToOpening = MathF.Sqrt(bestDistSq);

            // If the light is not directly at the opening, raycast ONLY ONCE to the closest opening
            if (distToOpening > 0.5f)
            {
                var dir = (bestOpening - lightPos) / distToOpening;
                var ray = new CollisionRay(lightPos, dir, (int)CollisionGroup.Opaque);
                var occluded = false;
                foreach (var _ in _physics.IntersectRayWithPredicate(sourceMapId, ray, lightUid,
                    (uid, ignored) => uid == ignored || !_occluderQuery.TryComp(uid, out var o) || !o.Enabled,
                    distToOpening, returnOnFirstHit: true))
                {
                    occluded = true;
                    break;
                }

                if (occluded)
                    continue;
            }

            // Light enters the adjacent level AT THE OPENING, not through solid floor!
            var proxyPos = distToOpening <= 0.75f ? lightPos : bestOpening;
            var falloff = Math.Clamp(1f - (distToOpening / lightComp.Radius), 0.15f, 1f);
            var proxyRadius = Math.Clamp(lightComp.Radius - (distToOpening * 0.4f), 0.5f, lightComp.Radius);
            var proxyEnergy = lightComp.Energy * AttenuationFactor * falloff;

            _visitedProxies.Add((lightUid, targetDepth));
            EnsureProxy(lightUid, lightComp, proxyPos, proxyRadius, proxyEnergy, targetMapId, sourceDepth, targetDepth);
        }
    }

    private void EnsureProxy(
        EntityUid sourceUid,
        PointLightComponent sourceLight,
        Vector2 worldPos,
        float proxyRadius,
        float proxyEnergy,
        MapId targetMapId,
        int sourceDepth,
        int targetDepth)
    {
        if (!_proxiesBySource.TryGetValue(sourceUid, out var proxies))
        {
            proxies = new List<EntityUid>();
            _proxiesBySource[sourceUid] = proxies;
        }

        EntityUid? existingProxy = null;
        foreach (var p in proxies)
        {
            if (TryComp<ZLevelLightProxyComponent>(p, out var proxyComp) && proxyComp.TargetDepth == targetDepth)
            {
                existingProxy = p;
                break;
            }
        }

        var targetCoords = new MapCoordinates(worldPos, targetMapId);
        if (existingProxy is { } proxy && Exists(proxy) && !TerminatingOrDeleted(proxy))
        {
            _transform.SetWorldPosition(proxy, worldPos);
            if (_lightQuery.TryComp(proxy, out var pLight))
            {
                _pointLight.SetColor(proxy, sourceLight.Color, pLight);
                _pointLight.SetRadius(proxy, proxyRadius, pLight);
                _pointLight.SetEnergy(proxy, proxyEnergy, pLight);
                _pointLight.SetCastShadows(proxy, true, pLight);
                _pointLight.SetEnabled(proxy, sourceLight.Enabled, pLight);
            }
        }
        else
        {
            EntityUid newProxy;
            while (_proxyPool.TryDequeue(out var pooled))
            {
                if (Exists(pooled) && !TerminatingOrDeleted(pooled))
                {
                    newProxy = pooled;
                    _transform.SetMapCoordinates(newProxy, targetCoords);
                    goto ConfigureProxy;
                }
            }

            newProxy = Spawn(null, targetCoords);

        ConfigureProxy:
            var proxyComp = EnsureComp<ZLevelLightProxyComponent>(newProxy);
            proxyComp.SourceLight = sourceUid;
            proxyComp.SourceDepth = sourceDepth;
            proxyComp.TargetDepth = targetDepth;
            var pLight = EnsureComp<PointLightComponent>(newProxy);
            _pointLight.SetColor(newProxy, sourceLight.Color, pLight);
            _pointLight.SetRadius(newProxy, proxyRadius, pLight);
            _pointLight.SetEnergy(newProxy, proxyEnergy, pLight);
            _pointLight.SetCastShadows(newProxy, true, pLight);
            _pointLight.SetEnabled(newProxy, sourceLight.Enabled, pLight);
            proxies.Add(newProxy);
        }
    }
}
