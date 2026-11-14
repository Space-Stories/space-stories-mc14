using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Areas;

public sealed partial class AreaSystem
{
    private readonly HashSet<Vector2i> _tacticalMapHiddenTiles = new();

    public static bool TryGetTacticalMapBounds(AreaGridComponent grid, out Vector2i minimum, out Vector2i maximum)
    {
        minimum = default;
        maximum = default;
        var found = false;
        foreach (var position in grid.Colors.Keys)
        {
            if (!found)
            {
                minimum = position;
                maximum = position;
                found = true;
            }
            else
            {
                minimum = Vector2i.ComponentMin(minimum, position);
                maximum = Vector2i.ComponentMax(maximum, position);
            }
        }

        if (!found || !grid.HasTacMapBounds)
            return found;

        var clippedMin = Vector2i.ComponentMax(minimum, grid.TacMapBoundsMin);
        var clippedMax = Vector2i.ComponentMin(maximum, grid.TacMapBoundsMax);
        if (clippedMax.X >= clippedMin.X && clippedMax.Y >= clippedMin.Y)
        {
            minimum = clippedMin;
            maximum = clippedMax;
        }

        return true;
    }

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
