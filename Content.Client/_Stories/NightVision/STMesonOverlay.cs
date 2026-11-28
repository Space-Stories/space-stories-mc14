using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Stories.NightVision;

public sealed class STMesonOverlay : Overlay
{
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly Color WallColor = new(0.8f, 0.8f, 0.8f, 0.55f);
    private static readonly Color FloorColor = new(1f, 1f, 1f, 0.6f);
    private static readonly TimeSpan VisibilityRefresh = TimeSpan.FromSeconds(0.3);

    private static readonly Vector2[] SampleOffsets =
    {
        Vector2.Zero,
        new(-0.4f, -0.4f),
        new(0.4f, -0.4f),
        new(-0.4f, 0.4f),
        new(0.4f, 0.4f),
    };

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;

    private readonly ExamineSystemShared _examine;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _map;
    private readonly SpriteSystem _sprite;
    private readonly TagSystem _tag;
    private readonly SharedTransformSystem _transform;

    private readonly HashSet<EntityUid> _entities = new();
    private readonly List<(EntityUid Uid, SpriteComponent Sprite)> _walls = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _wallTiles = new();
    private readonly Dictionary<int, Texture?> _tileTextures = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), bool> _hiddenTiles = new();
    private List<Entity<MapGridComponent>> _grids = new();

    private MapCoordinates _origin;
    private Vector2i? _originTile;
    private TimeSpan _nextRefresh;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public STMesonOverlay()
    {
        IoCManager.InjectDependencies(this);

        _examine = _entity.System<ExamineSystemShared>();
        _lookup = _entity.System<EntityLookupSystem>();
        _map = _entity.System<SharedMapSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _tag = _entity.System<TagSystem>();
        _transform = _entity.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye || eye != _eye.CurrentEye)
            return;

        UpdateOrigin(eye.Position);

        var handle = args.WorldHandle;

        CollectWalls(args.MapId, args.WorldAABB);
        DrawFloors(handle, args.MapId, args.WorldAABB);
        DrawWalls(handle, args.Viewport.Eye?.Rotation ?? Angle.Zero);

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void UpdateOrigin(MapCoordinates origin)
    {
        var mapChanged = origin.MapId != _origin.MapId;
        _origin = origin;

        Vector2i? originTile = null;
        if (_mapManager.TryFindGridAt(origin, out var grid, out var gridComp))
            originTile = _map.TileIndicesFor(grid, gridComp, origin);

        var time = _timing.RealTime;
        if (!mapChanged && originTile == _originTile && time < _nextRefresh)
            return;

        _originTile = originTile;
        _nextRefresh = time + VisibilityRefresh;
        _hiddenTiles.Clear();
    }

    private void DrawFloors(DrawingHandleWorld handle, MapId mapId, Box2 bounds)
    {
        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapId, bounds, ref _grids);

        foreach (var grid in _grids)
        {
            handle.SetTransform(_transform.GetWorldMatrix(grid.Owner));

            var tileSize = grid.Comp.TileSize;
            foreach (var tileRef in _map.GetTilesIntersecting(grid.Owner, grid.Comp, bounds))
            {
                if (GetTileTexture(tileRef.Tile.TypeId) is not { } texture)
                    continue;

                if (_wallTiles.Contains((grid.Owner, tileRef.GridIndices)) || !IsHidden(grid, tileRef.GridIndices))
                    continue;

                var size = texture.Height;
                var region = new UIBox2(tileRef.Tile.Variant * size, 0, (tileRef.Tile.Variant + 1) * size, size);
                var quad = Box2.FromDimensions(tileRef.GridIndices * tileSize, new Vector2(tileSize, tileSize));
                handle.DrawTextureRectRegion(texture, quad, FloorColor, region);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void CollectWalls(MapId mapId, Box2 bounds)
    {
        _walls.Clear();
        _wallTiles.Clear();
        _entities.Clear();
        _lookup.GetEntitiesIntersecting(mapId, bounds, _entities, LookupFlags.Static | LookupFlags.Approximate);

        foreach (var uid in _entities)
        {
            if (!_tag.HasTag(uid, WallTag))
                continue;

            if (!_entity.TryGetComponent(uid, out SpriteComponent? sprite) || !sprite.Visible)
                continue;

            var xform = _entity.GetComponent<TransformComponent>(uid);
            if (xform.GridUid is not { } grid || !_entity.TryGetComponent(grid, out MapGridComponent? gridComp))
                continue;

            var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            _wallTiles.Add((grid, tile));

            if (IsHidden((grid, gridComp), tile))
                _walls.Add((uid, sprite));
        }
    }

    private void DrawWalls(DrawingHandleWorld handle, Angle eyeRotation)
    {
        foreach (var (uid, sprite) in _walls)
        {
            var (position, rotation) = _transform.GetWorldPositionRotation(uid);
            var color = sprite.Color;

            _sprite.SetColor((uid, sprite), color * WallColor);
            _sprite.RenderSprite((uid, sprite), handle, eyeRotation, rotation, position);
            _sprite.SetColor((uid, sprite), color);
        }
    }

    private bool IsHidden(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (_hiddenTiles.TryGetValue((grid.Owner, tile), out var hidden))
            return hidden;

        hidden = true;
        var center = _map.GridTileToWorldPos(grid.Owner, grid.Comp, tile);
        foreach (var offset in SampleOffsets)
        {
            var point = new MapCoordinates(center + offset, _origin.MapId);
            if (_examine.InRangeUnOccluded(_origin, point, 0f, null))
            {
                hidden = false;
                break;
            }
        }

        _hiddenTiles[(grid.Owner, tile)] = hidden;
        return hidden;
    }

    private Texture? GetTileTexture(int typeId)
    {
        if (_tileTextures.TryGetValue(typeId, out var cached))
            return cached;

        Texture? texture = null;
        if (_tileDefinitions[typeId] is ContentTileDefinition { Sprite: { } path })
            texture = _resourceCache.GetResource<TextureResource>(path).Texture;

        _tileTextures[typeId] = texture;
        return texture;
    }
}
