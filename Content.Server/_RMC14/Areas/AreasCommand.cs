using Content.Server.Administration; // Stories-ZLevels
using Content.Shared._RMC14.Areas;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RMC14.Areas;

[ToolshedCommand, AdminCommand(AdminFlags.Mapping)] // Stories-Admins
public sealed class AreasCommand : ToolshedCommand
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private AreaSystem? _area; // Stories-ZLevels
    private MapSystem? _map;

    [CommandImplementation("save")]
    public void Save([CommandInvocationContext] IInvocationContext ctx)
    {
        _area = GetSys<AreaSystem>(); // Stories-ZLevels
        _map = GetSys<MapSystem>();

        var gridQuery = GetEntityQuery<MapGridComponent>();
        // Stories-ZLevels-Start
        var savedAreas = new Dictionary<EntityUid, HashSet<Vector2i>>();

        var toSave = new List<(EntityUid Uid, EntityUid GridId, MapGridComponent Grid, string ProtoId, Robust.Shared.Map.EntityCoordinates Coords)>();
        // Stories-ZLevels-End

        var query = EntityManager.AllEntityQueryEnumerator<AreaComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var metaData, out var xform))
        {
            if (xform.GridUid is not { } gridId ||
                !gridQuery.TryComp(gridId, out var grid))
            {
                continue;
            }

            if (metaData.EntityPrototype is not { } prototype)
            {
                ctx.WriteLine($"{EntityManager.ToPrettyString(uid)} did not have a prototype.");
                continue;
            }

            // Stories-ZLevels-Start
            toSave.Add((uid, gridId, grid, prototype.ID, xform.Coordinates));
        }

        foreach (var (uid, gridId, grid, protoId, coords) in toSave)
        {
            var areaGrid = EnsureComp<AreaGridComponent>(gridId);
            var indices = _map.TileIndicesFor(gridId, grid, coords);
            _area.SetArea(areaGrid, indices, protoId);

            if (!savedAreas.TryGetValue(gridId, out var set))
            {
                set = new HashSet<Vector2i>();
                savedAreas[gridId] = set;
            }
            set.Add(indices);

            // Stories-ZLevels-End
            QDel(uid);
        // Stories-ZLevels-Start
        }

        // Clean up areas in space (off-tile) where the area entity was deleted.
        var gridsToClean = new List<(EntityUid GridId, AreaGridComponent AreaGrid, MapGridComponent Grid)>();
        var areaGridQueryEnum = EntityManager.AllEntityQueryEnumerator<AreaGridComponent, MapGridComponent>();
        while (areaGridQueryEnum.MoveNext(out var gridId, out var areaGrid, out var grid))
        {
            gridsToClean.Add((gridId, areaGrid, grid));
        }

        foreach (var (gridId, areaGrid, grid) in gridsToClean)
        {
            savedAreas.TryGetValue(gridId, out var set);
            var toRemove = new List<Vector2i>();

            foreach (var pos in areaGrid.Areas.Keys)
            {
                var isOffTile = !_map.TryGetTileRef(gridId, grid, pos, out var tileRef) || tileRef.Tile.IsEmpty;
                if (isOffTile && (set == null || !set.Contains(pos)))
                {
                    toRemove.Add(pos);
                }
            }

            foreach (var pos in toRemove)
            {
                _area.RemoveArea(areaGrid, pos);
            }

            _area.ClearAreaEntities((gridId, areaGrid));
        // Stories-ZLevels-End
        }
    }

    [CommandImplementation("load")]
    public void Load()
    {
        Load(_ => true);
    }

    [CommandImplementation("loadmortar")]
    public void LoadMortar()
    {
        Load(a => a.MortarFire);
    }

    private void Load(Predicate<AreaComponent> predicate)
    {
        _map = GetSys<MapSystem>();

        var query = EntityManager.AllEntityQueryEnumerator<AreaGridComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var areas, out var mapGrid, out var xform))
        {
            foreach (var (position, protoId) in areas.Areas)
            {
                if (!_prototypes.TryIndex(protoId, out var proto))
                    continue;

                if (!proto.TryGetComponent(out AreaComponent? areaComp, _compFactory))
                    continue;

                if (!predicate(areaComp))
                    continue;

                var coordinates = _map.ToCenterCoordinates(uid, position, mapGrid); // Stories-ZLevels
                Spawn(protoId, coordinates);
            }
        }
    }
}
