using System.Numerics;
using Robust.Shared.Analyzers;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared.Maps;
using Content.Shared.Toggleable;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared._Stories.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem
{
    public bool TryFindGridAt(EntityUid mapUid, Vector2 worldPos, out EntityUid gridUid, [NotNullWhen(true)] out MapGridComponent? grid)
    {
        gridUid = default;
        grid = null;
        if (!_mapQuery.TryComp(mapUid, out var mapComp))
            return false;

        // Try exact tile first
        if (_mapManager.TryFindGridAt(mapComp.MapId, worldPos, out gridUid, out grid))
            return true;

        // Fall back to broadphase bounds so we find the grid even if this particular tile is empty!
        var box = Box2.CenteredAround(worldPos, new Vector2(1f, 1f));
        foreach (var candidate in _mapManager.FindGridsIntersecting(mapComp.MapId, box))
        {
            gridUid = candidate.Owner;
            grid = candidate;
            return true;
        }

        return false;
    }

    public bool IsTileTransparentOrEmpty(EntityUid mapUid, Vector2 worldPos)
    {
        if (!TryFindGridAt(mapUid, worldPos, out var gridUid, out var grid))
            return true;

        if (!_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        var tileDef = (ContentTileDefinition)_tileDef[tileRef.Tile.TypeId];
        return tileDef.Transparent;
    }

    /// <summary>
    /// Checks whether an opening (empty tile, transparent tile, or grid void) exists
    /// on the barrier floor of upperMapUid within maxRadius of worldPos.
    /// If found, returns true and the world position of the opening.
    /// </summary>
    public bool TryFindOpeningBetweenLevels(
        EntityUid upperMapUid,
        Vector2 worldPos,
        float maxRadius,
        out Vector2 openingPos)
    {
        openingPos = worldPos;
        if (!_gridQuery.TryComp(upperMapUid, out var grid) &&
            !TryFindGridAt(upperMapUid, worldPos, out _, out grid))
        {
            // If upper map has no grid at all, it's open space/void
            return true;
        }

        // 1. Direct check at worldPos
        if (IsTileTransparentOrEmpty(upperMapUid, worldPos))
        {
            openingPos = worldPos;
            return true;
        }

        // 2. Search for any opening tile within maxRadius
        var gridUid = grid.Owner;
        var tileSize = grid.TileSize;
        var radiusInTiles = Math.Clamp((int)MathF.Ceiling(maxRadius / tileSize), 1, 15);
        var centerTile = _map.WorldToTile(gridUid, grid, worldPos);

        var bestDistSq = float.MaxValue;
        var found = false;

        for (var dx = -radiusInTiles; dx <= radiusInTiles; dx++)
        {
            for (var dy = -radiusInTiles; dy <= radiusInTiles; dy++)
            {
                var tile = centerTile + new Vector2i(dx, dy);
                var tilePos = _map.GridTileToWorld(gridUid, grid, tile).Position;
                var distSq = Vector2.DistanceSquared(worldPos, tilePos);
                if (distSq > maxRadius * maxRadius)
                    continue;

                // Check if this tile is an opening (empty or transparent)
                if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) ||
                    tileRef.Tile.IsEmpty ||
                    ((ContentTileDefinition)_tileDef[tileRef.Tile.TypeId]).Transparent)
                {
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        openingPos = tilePos;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    private void InitializeView()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleLookUp, InputCmdHandler.FromDelegate(HandleToggleLookUp, handle: false))
            .Register<SharedZLevelsSystem>();

        SubscribeLocalEvent<ZLevelViewerComponent, ToggleActionEvent>(OnToggleLookUp);
        SubscribeLocalEvent<ZLevelViewerComponent, ToggleLookUpAlertEvent>(OnToggleLookUpAlert);
        SubscribeLocalEvent<ZLevelViewerComponent, MoveEvent>(OnViewerMove);
        SubscribeLocalEvent<ZLevelViewerComponent, EntParentChangedMessage>(OnViewerParentChanged);
        SubscribeLocalEvent<ZLevelViewerComponent, ComponentStartup>(OnViewerStartup);
        SubscribeLocalEvent<ZLevelViewerComponent, ComponentShutdown>(OnViewerShutdown);
    }

    private void OnViewerStartup(Entity<ZLevelViewerComponent> entity, ref ComponentStartup args)
    {
        UpdateLookUpAlert(entity);
    }

    private void OnViewerShutdown(Entity<ZLevelViewerComponent> entity, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(entity, LookUpAlert);
    }

    protected virtual void OnViewerMove(Entity<ZLevelViewerComponent> entity, ref MoveEvent args)
    {
        UpdateLookUpAlert(entity);
    }

    private void OnViewerParentChanged(Entity<ZLevelViewerComponent> entity, ref EntParentChangedMessage args)
    {
        UpdateLookUpAlert(entity);
    }

    private void HandleToggleLookUp(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } player)
            return;

        if (TryComp<ZLevelViewerComponent>(player, out var viewer))
        {
            ToggleLookUp((player, viewer));
        }
    }

    [PublicAPI]
    public void ToggleLookUp(Entity<ZLevelViewerComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return;

        if (!CanLookUp(entity.Owner))
        {
            if (entity.Comp.LookUp)
            {
                entity.Comp.LookUp = false;
                DirtyField(entity, entity.Comp, nameof(ZLevelViewerComponent.LookUp));
                if (entity.Comp.ActionEntity != null)
                    _actions.SetToggled(entity.Comp.ActionEntity, false);
            }
            UpdateLookUpAlert((entity.Owner, entity.Comp));
            return;
        }

        entity.Comp.LookUp = !entity.Comp.LookUp;
        DirtyField(entity, entity.Comp, nameof(ZLevelViewerComponent.LookUp));
        if (entity.Comp.ActionEntity != null)
            _actions.SetToggled(entity.Comp.ActionEntity, entity.Comp.LookUp);
        UpdateLookUpAlert((entity.Owner, entity.Comp));
    }

    private void OnToggleLookUp(Entity<ZLevelViewerComponent> entity, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;
        if (entity.Comp.ActionEntity != null && args.Action.Owner != entity.Comp.ActionEntity)
            return;
        args.Handled = true;

        ToggleLookUp((entity.Owner, entity.Comp));
    }

    private void OnToggleLookUpAlert(Entity<ZLevelViewerComponent> entity, ref ToggleLookUpAlertEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        ToggleLookUp((entity.Owner, entity.Comp));
    }

    [PublicAPI]
    public bool CanLookUp(EntityUid player, TransformComponent? xform = null)
    {
        if (!Resolve(player, ref xform, false))
            return false;

        if (xform.MapUid is not { } mapUid ||
            !_zMapQuery.TryComp(mapUid, out var zMapComp) ||
            !TryMapUp((mapUid, zMapComp), out _))
        {
            return false;
        }

        return !HasRoofAbove(player, xform);
    }

    [PublicAPI]
    public void UpdateLookUpAlert(Entity<ZLevelViewerComponent> entity)
    {
        if (!TryComp<TransformComponent>(entity, out var xform))
            return;

        var canLookUp = CanLookUp(entity, xform);
        if (!canLookUp && entity.Comp.LookUp)
        {
            entity.Comp.LookUp = false;
            DirtyField(entity, entity.Comp, nameof(ZLevelViewerComponent.LookUp));
            if (entity.Comp.ActionEntity != null)
                _actions.SetToggled(entity.Comp.ActionEntity, false);
        }

        short severity = (short)(canLookUp ? 1 : 0);
        _alerts.ShowAlert(entity, LookUpAlert, severity);
    }

    [PublicAPI]
    public bool HasRoofAbove(EntityUid player, TransformComponent? xform = null)
    {
        if (!Resolve(player, ref xform, false))
            return false;

        var worldPos = _transform.GetWorldPosition(xform);
        if (_zMapQuery.TryComp(xform.MapUid, out var zMapComp))
        {
            if (!TryMapUp((xform.MapUid.Value, zMapComp), out _))
                return true;

            // If the layer above does not block sight (open space, hole, or transparent tile), then there is NO roof!
            // Check center first, then cardinal offsets for character radius (~0.35m)
            var currentMap = (xform.MapUid.Value, zMapComp);
            if (!HasOpaqueAbove(worldPos, currentMap) ||
                !HasOpaqueAbove(new Vector2(worldPos.X + 0.35f, worldPos.Y), currentMap) ||
                !HasOpaqueAbove(new Vector2(worldPos.X - 0.35f, worldPos.Y), currentMap) ||
                !HasOpaqueAbove(new Vector2(worldPos.X, worldPos.Y + 0.35f), currentMap) ||
                !HasOpaqueAbove(new Vector2(worldPos.X, worldPos.Y - 0.35f), currentMap))
            {
                return false;
            }

            return true;
        }

        if (xform.GridUid is { } gridUid &&
            _gridQuery.TryComp(gridUid, out var grid) &&
            _roofQuery.TryComp(gridUid, out var roofComp))
        {
            var tile = _map.WorldToTile(gridUid, grid, worldPos);
            if (_roof.IsRooved((gridUid, grid, roofComp), tile))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates how many z-levels above the entity's current position are visible (i.e. not blocked by an opaque tile),
    /// up to <see cref="MaxZLevelsAboveRendering"/>.
    /// </summary>
    public int GetVisibleZLevelsAbove(EntityUid ent, Entity<ZMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;
        if (currentMapUid is null)
            return 0;
        if (!TryMapUp(currentMapUid.Value, out var checkingMap))
            return 0;
        var worldPos = _transform.GetWorldPosition(ent);
        var visibleLevels = 0;
        for (var i = 1; i <= MaxZLevelsAboveRendering; i++)
        {
            // No grid or no chunk at this position means there's simply nothing there (open space),
            // not an opaque roof, so it shouldn't block sight upward. Mirrors HasOpaqueAbove below.
            if (TryFindGridAt(checkingMap, worldPos, out var gridUid, out var grid) &&
                _map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
            {
                var tileDef = (ContentTileDefinition)_tileDef[tileRef.Tile.TypeId];
                if (!tileDef.Transparent)
                    break;
            }

            visibleLevels++;
            if (i == MaxZLevelsAboveRendering || !TryMapUp(checkingMap.AsNullable(), out checkingMap))
                break;
        }

        return visibleLevels;
    }

    /// <summary>
    /// Checks whether any grid on the map above has an opaque (non-transparent) tile at the given world position.
    /// </summary>
    [PublicAPI]
    public bool HasOpaqueAbove(Vector2 worldPos, Entity<ZMapComponent?> currentMap)
    {
        if (!TryMapUp(currentMap, out var mapAboveUid))
            return false;
        if (!TryFindGridAt(mapAboveUid, worldPos, out var gridUid, out var grid))
            return false;
        if (!_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;
        return !((ContentTileDefinition)_tileDef[tileRef.Tile.TypeId]).Transparent;
    }

    public bool TryFindZShotOpening(
        EntityUid sourceMap,
        EntityUid targetMap,
        int offset,
        Vector2 from,
        Vector2 to,
        out Vector2 opening,
        bool preferOpeningAwayFromSource = false,
        float maxSourceDistanceFromOpeningEdgeTiles = float.PositiveInfinity)
    {
        opening = default;
        if (offset == 0)
            return false;
        var openingMap = offset < 0 ? sourceMap : targetMap;
        if (!_gridQuery.TryComp(openingMap, out var grid))
            return false;
        var sourceTile = preferOpeningAwayFromSource
            ? _map.WorldToTile(openingMap, grid, from)
            : default;
        var fallbackOpening = Vector2.Zero;
        var hasFallbackOpening = false;
        var maxSourceDistanceFromOpeningCenter = float.IsPositiveInfinity(maxSourceDistanceFromOpeningEdgeTiles)
            ? float.PositiveInfinity
            : grid.TileSize * (0.5f + Math.Max(0f, maxSourceDistanceFromOpeningEdgeTiles));
        var maxSourceDistanceSquared = maxSourceDistanceFromOpeningCenter * maxSourceDistanceFromOpeningCenter;
        var selectedOpening = Vector2.Zero;
        var localFrom = _map.WorldToLocal(openingMap, grid, from) / grid.TileSize;
        var localTo = _map.WorldToLocal(openingMap, grid, to) / grid.TileSize;
        var localDelta = localTo - localFrom;
        var currentTile = new Vector2i((int) MathF.Floor(localFrom.X), (int) MathF.Floor(localFrom.Y));
        var endTile = new Vector2i((int) MathF.Floor(localTo.X), (int) MathF.Floor(localTo.Y));
        var stepX = Math.Sign(localDelta.X);
        var stepY = Math.Sign(localDelta.Y);
        var tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.X);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.Y);
        var nextBoundaryX = stepX > 0 ? currentTile.X + 1f : currentTile.X;
        var nextBoundaryY = stepY > 0 ? currentTile.Y + 1f : currentTile.Y;
        var tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - localFrom.X) / localDelta.X;
        var tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - localFrom.Y) / localDelta.Y;
        while (true)
        {
            if (TryUseOpeningTile(currentTile))
            {
                opening = selectedOpening;
                return true;
            }

            if (currentTile == endTile)
                break;
            if (tMaxX < tMaxY)
            {
                currentTile += new Vector2i(stepX, 0);
                tMaxX += tDeltaX;
                continue;
            }

            if (tMaxY < tMaxX)
            {
                currentTile += new Vector2i(0, stepY);
                tMaxY += tDeltaY;
                continue;
            }

            currentTile += new Vector2i(stepX, stepY);
            tMaxX += tDeltaX;
            tMaxY += tDeltaY;
        }

        if (!hasFallbackOpening)
            return false;
        opening = fallbackOpening;
        return true;
        bool TryUseOpeningTile(Vector2i tile)
        {
            if (_map.TryGetTileRef(openingMap, grid, tile, out var tileRef) && !ZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tileDef))
                return false;
            var openingCenter = _map.ToCenterCoordinates(openingMap, tile, grid).Position;
            if (Vector2.DistanceSquared(from, openingCenter) > maxSourceDistanceSquared)
                return false;
            if (preferOpeningAwayFromSource && tile == sourceTile)
            {
                if (hasFallbackOpening)
                    return false;
                fallbackOpening = openingCenter;
                hasFallbackOpening = true;
                return false;
            }

            selectedOpening = openingCenter;
            return true;
        }
    }

    /// <summary>
    /// Checks if the entity or position is near a ladder or stairs leading to another Z-level.
    /// </summary>
    [PublicAPI]
    public bool IsNearLadderOrStairs(EntityUid user, TransformComponent? xform = null, float maxDist = 2.0f)
    {
        if (!Resolve(user, ref xform, false))
            return false;

        return IsNearLadderOrStairs(_transform.GetMapCoordinates(user, xform), maxDist);
    }

    /// <summary>
    /// Checks if the map coordinates are near a ladder or stairs leading to another Z-level.
    /// </summary>
    [PublicAPI]
    public bool IsNearLadderOrStairs(MapCoordinates originCoords, float maxDist = 2.0f)
    {
        if (originCoords.MapId == MapId.Nullspace)
            return false;

        _ladderLookupEntities.Clear();
        var box = Box2.CenteredAround(originCoords.Position, new Vector2(maxDist * 2, maxDist * 2));
        _lookup.GetEntitiesIntersecting(originCoords.MapId, box, _ladderLookupEntities, LookupFlags.Approximate | LookupFlags.Dynamic | LookupFlags.Static);

        foreach (var uid in _ladderLookupEntities)
        {
            if (_zLadderQuery.HasComp(uid) || _rmcLadderQuery.HasComp(uid) || _zHighGroundQuery.HasComp(uid))
            {
                var targetPos = _transform.GetWorldPosition(uid);
                if (Vector2.Distance(originCoords.Position, targetPos) <= maxDist)
                    return true;
            }
        }

        return false;
    }
}
