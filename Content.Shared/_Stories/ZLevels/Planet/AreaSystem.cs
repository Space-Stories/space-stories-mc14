using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Areas;

public sealed partial class AreaSystem
{
    /// <summary>
    /// Geometry-only upper levels inherit the main map's area restrictions until they have their own area grid.
    /// Unmapped underground levels remain inaccessible to support.
    /// </summary>
    private bool TryGetUpperPlanetArea(EntityCoordinates coordinates,
        [NotNullWhen(true)] out Entity<AreaComponent>? area,
        [NotNullWhen(true)] out EntityPrototype? areaPrototype)
    {
        area = null;
        areaPrototype = null;
        var map = _transform.GetMap(coordinates);
        if (!HasComp<RMCPlanetComponent>(map) ||
            !TryComp<ZMapComponent>(map, out var zMap) || zMap.Depth <= 0 ||
            !TryComp<ZMapNetworkComponent>(zMap.NetworkUid, out var network) ||
            !network.ZLevels.TryGetValue(0, out var mainMap) || mainMap is not { } main ||
            !HasComp<RMCPlanetComponent>(main) ||
            TerminatingOrDeleted(main) || !_mapGridQuery.TryComp(main, out var grid) ||
            !_areaGridQuery.TryComp(main, out var areas))
        {
            return false;
        }

        var projected = new MapCoordinates(_transform.ToMapCoordinates(coordinates).Position,
            _transform.GetMapId(main));
        var indices = _map.CoordinatesToTile(main, grid, _transform.ToCoordinates(projected));
        return TryGetArea((main, grid, areas), indices, out area, out areaPrototype);
    }
}
