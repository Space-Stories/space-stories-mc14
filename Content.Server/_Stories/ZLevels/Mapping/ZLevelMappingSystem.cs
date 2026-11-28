using Content.Server._Stories.ZLevels.Core;
using Content.Shared._Stories.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Stories.ZLevels.Mapping;

public sealed partial class ZLevelMappingSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ZLevelsSystem _zLevels = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZMapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZMapComponent, MapAddedIntoZNetworkEvent>(OnAddedIntoZNetwork);
    }

    private void OnAddedIntoZNetwork(Entity<ZMapComponent> ent, ref MapAddedIntoZNetworkEvent args)
    {
        if (_map.IsInitialized(ent))
            EntityManager.AddComponents(ent, args.Network.Comp.Components);
        else
        {
            var hasInitializedMaps = false;
            foreach (var existingMapUid in args.Network.Comp.ZLevels.Values)
            {
                if (existingMapUid.HasValue && _map.IsInitialized(existingMapUid.Value))
                {
                    hasInitializedMaps = true;
                    break;
                }
            }

            if (hasInitializedMaps)
                _map.InitializeMap(ent.Owner);
        }
    }

    private void OnMapInit(Entity<ZMapComponent> ent, ref MapInitEvent args)
    {
        if (!_zLevels.TryGetMapNetwork(ent, out var network))
            return;

        EntityManager.AddComponents(ent, network.Comp.Components);
    }
}
