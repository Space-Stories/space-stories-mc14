using System.Linq;
using Content.Server._Stories.ZLevels.Core;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Roof;
using Content.Shared.Light.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Stories.ZLevels.Roof;

public sealed partial class ZLevelsRoofSystem
{
    private void OnMapNetworkUpdated(ZLevelMapNetworkUpdatedEvent args)
    {
        if (!TryComp<ZMapNetworkComponent>(args.Network, out var network))
            return;

        RecalculateMapRoofs((args.Network, network));
    }

    /// <summary>
    /// The marker can be (re)created after the network was built, e.g. when z-network components are added
    /// on MapInit of a map that was uninitialized at network creation, wiping the weather mask.
    /// </summary>
    private void OnZLevelRoofStartup(Entity<ZLevelRoofComponent> ent, ref ComponentStartup args)
    {
        if (!ZMapQuery.HasComp(ent) || !_zLevels.TryGetMapNetwork(ent.Owner, out var network))
            return;

        RecalculateMapRoofs(network);
    }

    /// <summary>
    /// Planetary map path: propagate roof and weather state down through the z-map network.
    /// </summary>
    private void OnMapTileChanged(
        EntityUid mapUid,
        MapGridComponent currentMapGrid,
        RoofComponent currentRoof,
        ZMapComponent zLevelMapComp,
        TileChangedEvent args)
    {
        var mapsBelow = _zLevels.GetAllMapsBelow((mapUid, zLevelMapComp));
        if (mapsBelow.Count == 0)
            return;

        ZRoofQuery.TryComp(mapUid, out var currentZRoof);

        var covers = new (Vector2i Indices, bool Shadow, bool Weather)[args.Changes.Length];
        for (var i = 0; i < args.Changes.Length; i++)
        {
            var change = args.Changes[i];
            var shadow = _roof.IsRooved((mapUid, currentMapGrid, currentRoof), change.GridIndices) ||
                         CastsShadow(change.NewTile);
            var weather = IsWeatherBlocked((mapUid, currentZRoof), change.GridIndices) ||
                          BlocksWeather(change.NewTile);
            covers[i] = (change.GridIndices, shadow, weather);
        }

        // Nearest first: each level's own tile re-shields everything further down.
        foreach (var mapBelow in mapsBelow)
        {
            if (!GridQuery.TryComp(mapBelow, out var mapGridBelow))
                continue;

            var roofBelow = EnsureComp<RoofComponent>(mapBelow);
            ZRoofQuery.TryComp(mapBelow, out var zRoofBelow);

            for (var i = 0; i < covers.Length; i++)
            {
                ref var cover = ref covers[i];

                _roof.SetRoof((mapBelow, mapGridBelow, roofBelow), cover.Indices, cover.Shadow);
                if (zRoofBelow is not null)
                    SetWeatherBlocked((mapBelow, zRoofBelow), cover.Indices, cover.Weather);

                if (!_map.TryGetTile(mapGridBelow, cover.Indices, out var tile))
                    continue;

                cover.Shadow |= CastsShadow(tile);
                cover.Weather |= BlocksWeather(tile);
            }
        }
    }

    private void RecalculateMapRoofs(Entity<ZMapNetworkComponent> network)
    {
        _roofMap.Clear();
        _weatherMap.Clear();

        var sortedMaps = new List<EntityUid>();
        foreach (var mapUid in network.Comp.ZLevels
                     .OrderByDescending(kv => kv.Key) // depth sorting
                     .Select(kv => kv.Value)
                     .Where(uid => uid.HasValue)
                     .Select(uid => uid!.Value))
        {
            sortedMaps.Add(mapUid);
        }

        foreach (var map in sortedMaps)
        {
            if (!GridQuery.TryComp(map, out var mapGrid))
                continue;

            // Everything accumulated from the levels above covers this one, including its empty tiles
            // (e.g. an atrium hole under a glass roof).
            var roofComp = EnsureComp<RoofComponent>(map);
            roofComp.Data.Clear();
            Dirty(map, roofComp);
            foreach (var indices in _roofMap)
            {
                _roof.SetRoof((map, mapGrid, roofComp), indices, true);
            }

            if (ZRoofQuery.TryComp(map, out var zRoof))
            {
                zRoof.WeatherData.Clear();
                Dirty(map, zRoof);
                foreach (var indices in _weatherMap)
                {
                    SetWeatherBlocked((map, zRoof), indices, true);
                }
            }

            var enumerator = _map.GetAllTilesEnumerator(map, mapGrid);
            while (enumerator.MoveNext(out var tileRef))
            {
                if (CastsShadow(tileRef.Value.Tile))
                    _roofMap.Add(tileRef.Value.GridIndices);

                if (BlocksWeather(tileRef.Value.Tile))
                    _weatherMap.Add(tileRef.Value.GridIndices);
            }
        }
    }
}
