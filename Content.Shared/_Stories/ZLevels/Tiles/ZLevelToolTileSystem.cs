using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Analyzers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.ZLevels.Tiles;

/// <summary>
/// Lets tools with <see cref="ZLevelToolTileComponent"/> deconstruct the tile on the z-level above
/// the wielder while they're looking up, instead of the tile they're standing on. Intercepts
/// <see cref="AfterInteractEvent"/> ahead of <see cref="SharedToolSystem"/> so upstream's own
/// floor-breaking logic never has to know this mode exists.
/// </summary>
public sealed partial class ZLevelToolTileSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly TileSystem _tiles = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turfs = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZLevelToolTileComponent, AfterInteractEvent>(OnAfterInteract, before: new[] { typeof(SharedToolSystem) });
        SubscribeLocalEvent<ZLevelToolTileComponent, ZLevelTileToolDoAfterEvent>(OnToolTileComplete);
    }

    private void OnAfterInteract(Entity<ZLevelToolTileComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target != null && !HasComp<PuddleComponent>(args.Target))
            return;
        if (!TryComp<ZLevelViewerComponent>(args.User, out var viewer) || !viewer.LookUp)
            return;
        UseToolOnTileAbove(ent, args.User, args.ClickLocation);
        args.Handled = true;
    }

    private void UseToolOnTileAbove(Entity<ZLevelToolTileComponent> ent, EntityUid user, EntityCoordinates clickLocation)
    {
        if (!TryComp<ToolTileCompatibleComponent>(ent, out var tileComp) || !TryComp<ToolComponent>(ent, out var tool))
            return;
        if (Transform(user).MapUid is not { } mapUid)
            return;
        if (!_zLevel.TryMapUp((mapUid, null), out var aboveMap))
            return;
        var worldPos = _transform.ToMapCoordinates(clickLocation).Position;
        if (!_zLevel.TryFindGridAt(aboveMap.Owner, worldPos, out var gridUid, out var grid))
            return;
        var tileIndices = _map.WorldToTile(gridUid, grid, worldPos);
        var tileRef = _map.GetTileRef(gridUid, grid, tileIndices);
        var tileDef = (ContentTileDefinition)_tileDefManager[tileRef.Tile.TypeId];
        if (!_tool.HasAnyQuality(ent, tileDef.DeconstructTools, tool))
        {
            // Telegraph which tool is required, same as ToolTileCompatibleComponent's floor variant.
            var toolNames = new List<string>();
            foreach (var toolQuality in tileDef.DeconstructTools)
            {
                if (_prototype.TryIndex<ToolQualityPrototype>(toolQuality, out var protoToolQuality))
                    toolNames.Add(Loc.GetString(protoToolQuality.ToolName));
            }

            if (toolNames.Count > 0)
            {
                var separator = " " + Loc.GetString("stories-floor-tile-tool-separator") + " ";
                var toolNamesString = string.Join(separator, toolNames);
                _popup.PopupCoordinates(
                    Loc.GetString("stories-floor-tile-wrong-tool", ("toolNames", toolNamesString)),
                    clickLocation,
                    user);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(tileDef.BaseTurf))
            return;
        if (tileComp.RequiresUnobstructed && _turfs.IsTileBlocked(gridUid, tileIndices, CollisionGroup.MobMask))
        {
            _popup.PopupCoordinates(Loc.GetString("stories-ceiling-tile-obstructed"), clickLocation, user);
            return;
        }

        if (!_interaction.InRangeUnobstructed(user, clickLocation, popup: false))
            return;
        var doAfterArgs = new ZLevelTileToolDoAfterEvent(GetNetEntity(gridUid), tileIndices);
        _tool.UseTool(ent, user, ent, tileComp.Delay, tool.Qualities, doAfterArgs, out _, toolComponent: tool);
    }

    private void OnToolTileComplete(Entity<ZLevelToolTileComponent> ent, ref ZLevelTileToolDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;
        if (!TryComp<ToolComponent>(ent, out var tool) || !TryComp<ToolTileCompatibleComponent>(ent, out var tileComp))
            return;
        var gridUid = GetEntity(args.Grid);
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            Log.Error("Attempted use tool on a non-existent grid?");
            return;
        }

        var tileRef = _map.GetTileRef(gridUid, grid, args.GridTile);
        var coords = _map.ToCoordinates(tileRef, grid);
        if (tileComp.RequiresUnobstructed && _turfs.IsTileBlocked(gridUid, tileRef.GridIndices, CollisionGroup.MobMask))
            return;
        var tileDef = (ContentTileDefinition)_tileDefManager[tileRef.Tile.TypeId];
        if (!_tool.HasAnyQuality(ent, tileDef.DeconstructTools, tool))
            return;

        // don't do this on the client or else the tile entity spawn mispredicts and looks horrible
        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        if (!_tiles.DeconstructTile(tileRef, spawnItem: false))
            return;
        DropItemOnLevelBelow(gridUid, tileRef.GridIndices, tileDef);
        _adminLogger.Add(
            LogType.LatticeCut,
            LogImpact.Medium,
            $"{ToPrettyString(args.User):player} used {ToPrettyString(ent)} to edit the ceiling tile at {coords}");
        args.Handled = true;
    }

    /// <summary>
    /// Spawns the deconstructed ceiling tile's item on the grid directly below (the wielder's own
    /// level) at the same tile position, positioned near the ceiling so it reads as having just
    /// fallen through the hole. Mirrors <c>ZCollapseSystem.DropTileItemBelow</c>, but isn't tied to
    /// <c>CEGridStabilityComponent</c> — any grid at that position works, not just ones participating
    /// in the stability network.
    /// </summary>
    private void DropItemOnLevelBelow(EntityUid gridUid, Vector2i tile, ContentTileDefinition tileDef)
    {
        var itemProto = tileDef.ItemDropPrototypeName;
        if (itemProto == null)
            return;
        if (Transform(gridUid).MapUid is not { } mapUid ||
            !_zLevel.TryMapDown((mapUid, null), out var belowMap) ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var worldPos = _map.GridTileToWorldPos(gridUid, grid, tile);
        if (!_zLevel.TryFindGridAt(belowMap.Owner, worldPos, out var belowGridUid, out var belowGrid))
            return; // nothing below to fall onto — the item is simply lost
        var belowTile = _map.WorldToTile(belowGridUid, belowGrid, worldPos);
        var item = Spawn(itemProto, _map.GridTileToLocal(belowGridUid, belowGrid, belowTile));
        _zLevel.SetZPosition(item, 0.9f);
        _transform.SetLocalRotationNoLerp(item, _random.NextAngle());
        if (TryComp<PhysicsComponent>(item, out var physics))
            _physics.ApplyLinearImpulse(item, _random.NextVector2(0f, 1.5f), body: physics);
    }
}

[Serializable, NetSerializable]
public sealed partial class ZLevelTileToolDoAfterEvent : DoAfterEvent
{
    public NetEntity Grid;
    public Vector2i GridTile;
    public ZLevelTileToolDoAfterEvent(NetEntity grid, Vector2i gridTile)
    {
        Grid = grid;
        GridTile = gridTile;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is ZLevelTileToolDoAfterEvent otherTile
               && Grid == otherTile.Grid
               && GridTile == otherTile.GridTile;
    }
}
