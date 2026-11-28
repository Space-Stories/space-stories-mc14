using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Stories.ZLevels.Roof;

/// <summary>
/// Systems that automatically covers tiles with roofs (or removes roofs)
/// if there is a tile on one of the levels above in the ZLevels Map or Grid network.
/// Roof data is calculated on the server only and networked to clients.
/// </summary>
public abstract partial class SharedZLevelsRoofSystem : EntitySystem
{
    [Dependency] protected readonly SharedMapSystem _map = default!;
    [Dependency] protected readonly SharedRoofSystem _roof = default!;
    [Dependency] protected readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] protected readonly SharedZLevelsSystem _zLevels = default!;

    protected EntityQuery<MapGridComponent> GridQuery;
    protected EntityQuery<RoofComponent> RoofQuery;
    protected EntityQuery<ZLevelRoofComponent> ZRoofQuery;
    protected EntityQuery<ZMapComponent> ZMapQuery;
    protected EntityQuery<TransformComponent> XformQuery;

    public override void Initialize()
    {
        base.Initialize();

        GridQuery = GetEntityQuery<MapGridComponent>();
        RoofQuery = GetEntityQuery<RoofComponent>();
        ZRoofQuery = GetEntityQuery<ZLevelRoofComponent>();
        ZMapQuery = GetEntityQuery<ZMapComponent>();
        XformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <summary>
    /// Whether a tile on one of the z-levels above shields this tile from weather.
    /// </summary>
    public bool IsWeatherBlocked(Entity<ZLevelRoofComponent?> ent, Vector2i index)
    {
        if (!ZRoofQuery.Resolve(ent, ref ent.Comp, false))
            return false;

        var chunkOrigin = SharedMapSystem.GetChunkIndices(index, ZLevelRoofComponent.ChunkSize);
        if (!ent.Comp.WeatherData.TryGetValue(chunkOrigin, out var bitMask))
            return false;

        var bitFlag = GetBitFlag(index);
        return (bitMask & bitFlag) == bitFlag;
    }

    protected void SetWeatherBlocked(Entity<ZLevelRoofComponent> ent, Vector2i index, bool value)
    {
        var chunkOrigin = SharedMapSystem.GetChunkIndices(index, ZLevelRoofComponent.ChunkSize);

        if (!ent.Comp.WeatherData.TryGetValue(chunkOrigin, out var chunkData))
        {
            if (!value)
                return;

            chunkData = 0;
        }

        var bitFlag = GetBitFlag(index);
        var newData = value ? chunkData | bitFlag : chunkData & ~bitFlag;
        if (newData == chunkData)
            return;

        ent.Comp.WeatherData[chunkOrigin] = newData;
        Dirty(ent);
    }

    /// <summary>
    /// Whether this tile casts a roof shadow on the z-levels below.
    /// </summary>
    protected bool CastsShadow(Tile tile)
    {
        return !tile.IsEmpty && !((ContentTileDefinition)_tileDef[tile.TypeId]).Transparent;
    }

    /// <summary>
    /// Whether this tile shields the z-levels below from weather.
    /// </summary>
    protected bool BlocksWeather(Tile tile)
    {
        return !tile.IsEmpty && ((ContentTileDefinition)_tileDef[tile.TypeId]).BlocksWeather;
    }

    private static ulong GetBitFlag(Vector2i index)
    {
        var chunkRelative = SharedMapSystem.GetChunkRelative(index, ZLevelRoofComponent.ChunkSize);
        return (ulong) 1 << (chunkRelative.X + chunkRelative.Y * ZLevelRoofComponent.ChunkSize);
    }
}
