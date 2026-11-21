using Content.Server._Stories.ZLevels.Core;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared._Stories.ZLevels.Roof;
using Robust.Shared.Map.Components;

namespace Content.Server._Stories.ZLevels.Roof;

/// <summary>
/// Server-only roof calculation. Clients must not recalculate networked roof data on their own:
/// they receive grid chunks of different z-levels in arbitrary order and would overwrite
/// server state with results computed from partial data.
/// </summary>
public sealed partial class ZLevelsRoofSystem : SharedZLevelsRoofSystem
{
    private readonly HashSet<Vector2i> _roofMap = new();
    private readonly HashSet<Vector2i> _weatherMap = new();

    public override void Initialize()
    {
        base.Initialize();

        _zgridQuery = GetEntityQuery<ZGridComponent>();
        _zGridNetworkQuery = GetEntityQuery<ZGridNetworkComponent>();

        SubscribeLocalEvent<ZLevelRoofComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZGridNetworkComponent, ZLevelGridNetworkUpdatedEvent>(OnZGridNetworkUpdate);
        SubscribeLocalEvent<ZGridComponent, MapInitEvent>(OnZGridMapInit);
        SubscribeLocalEvent<ZLevelMapNetworkUpdatedEvent>(OnMapNetworkUpdated);
        SubscribeLocalEvent<ZLevelRoofComponent, ComponentStartup>(OnZLevelRoofStartup);
    }

    private void OnTileChanged(Entity<ZLevelRoofComponent> ent, ref TileChangedEvent args)
    {
        if (!GridQuery.TryComp(ent, out var currentMapGrid))
            return;
        if (!RoofQuery.TryComp(ent, out var currentRoof))
            return;

        if (args.Changes.Length == 0)
            return;

        if (ZMapQuery.TryComp(ent, out var zLevelMapComp))
            OnMapTileChanged(ent, currentMapGrid, currentRoof, zLevelMapComp, args);
        else
            OnGridTileChanged(ent, currentMapGrid, currentRoof, args);
    }
}
