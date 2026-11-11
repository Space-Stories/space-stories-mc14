using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Areas;

public sealed partial class AreaSystem
{
    private readonly HashSet<Vector2i> _tacticalMapHiddenTiles = new();

    private void UpdateTacticalMapBounds(EntityUid gridUid, MapGridComponent grid, AreaGridComponent areas)
    {
        _tacticalMapHiddenTiles.Clear();
        areas.HasTacMapBounds = false;
        areas.TacMapBoundsMin = default;
        areas.TacMapBoundsMax = default;

        foreach (var position in areas.Colors.Keys)
        {
            if (!areas.Areas.TryGetValue(position, out var areaId) ||
                !areas.AreaEntities.TryGetValue(areaId, out var areaUid) ||
                !_areaQuery.TryComp(areaUid, out var area))
            {
                continue;
            }

            if (area.ExcludeFromTacMapRender)
                _tacticalMapHiddenTiles.Add(position);

            var tile = _map.GetTileRef(gridUid, grid, position);
            if (area.ExcludeFromTacMapBounds || tile.Tile.IsEmpty || _turf.IsSpace(tile))
                continue;

            if (!areas.HasTacMapBounds)
            {
                areas.HasTacMapBounds = true;
                areas.TacMapBoundsMin = position;
                areas.TacMapBoundsMax = position;
            }
            else
            {
                areas.TacMapBoundsMin = Vector2i.ComponentMin(areas.TacMapBoundsMin, position);
                areas.TacMapBoundsMax = Vector2i.ComponentMax(areas.TacMapBoundsMax, position);
            }
        }

        foreach (var position in _tacticalMapHiddenTiles)
        {
            areas.Colors.Remove(position);
            areas.Labels.Remove(position);
        }
    }
}
