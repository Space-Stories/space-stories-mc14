using Content.Shared._Stories.ZLevels.Core.Components;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Rules;

public sealed partial class RMCPlanetSystem
{
    /// <summary>
    /// Older coordinate-only callers still target the main planet level.
    /// </summary>
    private bool IsMainPlanetLevel(EntityUid planet)
    {
        return !TryComp<ZMapComponent>(_transform.GetMap(planet), out var zMap) || zMap.Depth == 0;
    }

    private Vector2i GetPlanetCoordinateOffset(Entity<RMCPlanetComponent> planet)
    {
        if (TryComp<ZMapComponent>(_transform.GetMap(planet.Owner), out var zMap) &&
            TryComp<ZMapNetworkComponent>(zMap.NetworkUid, out var network) &&
            network.ZLevels.TryGetValue(0, out var mainMap) &&
            TryComp<RMCPlanetComponent>(mainMap, out var mainPlanet))
        {
            return mainPlanet.Offset;
        }

        return planet.Comp.Offset;
    }

    public int GetPlanetZLevel(MapCoordinates coordinates)
    {
        var map = _transform.GetMap(_transform.ToCoordinates(coordinates));
        return TryComp<ZMapComponent>(map, out var zMap) ? zMap.Depth : 0;
    }

    public bool TryPlanetToCoordinates(Vector2i coordinates, int zLevel, out MapCoordinates mapCoordinates)
    {
        mapCoordinates = default;
        var planets = EntityQueryEnumerator<RMCPlanetComponent>();
        while (planets.MoveNext(out var uid, out var comp))
        {
            if (!IsMainPlanetLevel(uid) || TerminatingOrDeleted(uid))
                continue;

            var target = _transform.GetMap(uid);
            if (TryComp<ZMapComponent>(target, out var zMap))
            {
                if (!TryComp<ZMapNetworkComponent>(zMap.NetworkUid, out var network) ||
                    !network.ZLevels.TryGetValue(zLevel, out target) ||
                    !HasComp<RMCPlanetComponent>(target))
                {
                    continue;
                }
            }
            else if (zLevel != 0)
                continue;

            if (target is not { } targetMap || TerminatingOrDeleted(targetMap))
                continue;

            var offset = GetPlanetCoordinateOffset((uid, comp));
            mapCoordinates = new MapCoordinates(coordinates - offset, _transform.GetMapId(targetMap));
            return true;
        }

        return false;
    }
}
