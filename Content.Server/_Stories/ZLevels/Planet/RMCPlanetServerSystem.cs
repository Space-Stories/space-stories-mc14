using Content.Server._Stories.ZLevels.Core;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Stories.ZLevels.Planet;

public sealed class RMCPlanetServerSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly RMCPlanetSystem _planet = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCPlanetMapLoadedEvent>(OnMapLoaded);
    }

    private void OnMapLoaded(ref RMCPlanetMapLoadedEvent args)
    {
        if (!_planet.PlanetPaths.TryGetValue(args.Path.ToString(), out var protoId) ||
            !_prototypes.TryIndex(protoId, out var planetProto) ||
            !planetProto.TryGetComponent(out RMCPlanetMapPrototypeComponent? planetComp, _compFactory))
        {
            return;
        }

        if (planetComp.MapsBelow.Count == 0 && planetComp.MapsAbove.Count == 0)
            return;

        var options = new DeserializationOptions { InitializeMaps = true };
        var planetNetwork = _zLevels.CreateMapNetwork();
        var dict = new Dictionary<EntityUid, int> { { args.MapUid, 0 } };

        var depth = -planetComp.MapsBelow.Count;
        foreach (var mapBelow in planetComp.MapsBelow)
        {
            if (_mapLoader.TryLoadMap(mapBelow, out var mapEnt, out _, options) && mapEnt is { } map)
            {
                EnsureComp<RMCPlanetComponent>(map.Owner);
                EnsureComp<TacticalMapComponent>(map.Owner);
                dict.Add(map.Owner, depth);
            }
            depth++;
        }

        depth = 1;
        foreach (var mapAbove in planetComp.MapsAbove)
        {
            if (_mapLoader.TryLoadMap(mapAbove, out var mapEnt, out _, options) && mapEnt is { } map)
            {
                EnsureComp<RMCPlanetComponent>(map.Owner);
                EnsureComp<TacticalMapComponent>(map.Owner);
                dict.Add(map.Owner, depth);
            }
            depth++;
        }

        _zLevels.TryAddMapsIntoNetwork(planetNetwork, dict);
    }
}
