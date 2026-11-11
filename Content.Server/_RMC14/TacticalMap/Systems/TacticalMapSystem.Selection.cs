using System;
using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Marines;
using Content.Server._RMC14.Overwatch;
using Content.Server._RMC14.Xenonids.Watch;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.HiveLeader;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Traits.Assorted;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    protected override bool TryResolveUserMap(Entity<TacticalMapUserComponent> user, out Entity<TacticalMapComponent> map)
    {
        if (!TryResolveMap(user.Owner, user.Comp.Map, out map))
            return false;

        if (user.Comp.Map != map.Owner)
        {
            user.Comp.Map = map.Owner;
            Dirty(user);
        }

        return true;
    }

    protected override bool TryResolveComputerMap(Entity<TacticalMapComputerComponent> computer, out Entity<TacticalMapComponent> map)
    {
        if (!TryResolveMap(computer.Owner, computer.Comp.Map, out map))
            return false;

        if (computer.Comp.Map != map.Owner)
        {
            computer.Comp.Map = map.Owner;
            Dirty(computer);
        }

        return true;
    }

    private bool TryResolveMap(EntityUid owner, EntityUid? selectedMap, out Entity<TacticalMapComponent> map)
    {
        // prefer saved selection, then current grid, then any available tacmap.
        if (selectedMap != null && _tacticalMapQuery.TryComp(selectedMap.Value, out var selectedMapComp))
        {
            map = (selectedMap.Value, selectedMapComp);
            return true;
        }

        if (_transformQuery.TryComp(owner, out var xform))
        {
            if (xform.GridUid != null && _tacticalMapQuery.TryComp(xform.GridUid.Value, out var gridMap))
            {
                map = (xform.GridUid.Value, gridMap);
                return true;
            }

            if (xform.MapUid is { } mapUid && _tacticalMapQuery.TryComp(mapUid, out var mapComp))
            {
                map = (mapUid, mapComp);
                return true;
            }
        }

        return TryGetTacticalMap(out map);
    }

    private void OnUserBUIOpened(Entity<TacticalMapUserComponent> ent, ref BoundUIOpenedEvent args)
    {
        EnsureComp<ActiveTacticalMapUserComponent>(ent);
        RefreshUserVisibleLayers(ent);
        if (TryResolveUserMap(ent, out var map))
            UpdateUserData(ent, map.Comp);
        UpdateTacticalMapState(ent);
    }

    private void OnUserSquadMemberUpdated(Entity<TacticalMapUserComponent> ent, ref SquadMemberUpdatedEvent args)
    {
        RefreshUserVisibleLayers(ent);
        var options = ent.Comp.LayerOptions;
        var baseLayers = EnsureBaseLayers(ent);

        if (TryGetViewerSquadLayer(ent.Owner, out var squadLayer) &&
            options.Contains(squadLayer) &&
            !ent.Comp.VisibleLayers.Contains(squadLayer))
        {
            ent.Comp.VisibleLayers.Add(squadLayer);
            ApplyVisibleLayerSelection(ent, options, baseLayers);
        }

        if (TryResolveUserMap(ent, out var map))
            UpdateUserData(ent, map.Comp);
    }

    private void OnUserSquadMemberRemoved(Entity<TacticalMapUserComponent> ent, ref SquadMemberRemovedEvent args)
    {
        RefreshUserVisibleLayers(ent);
        if (TryResolveUserMap(ent, out var map))
            UpdateUserData(ent, map.Comp);
    }

    private void OnComputerBUIOpened(Entity<TacticalMapComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        EnsureComp<ActiveTacticalMapComputerComponent>(ent);
        RefreshComputerVisibleLayers(ent);
        if (_squad.TryGetMemberSquad(args.Actor, out var squad))
            ent.Comp.ObjectivesSquad = squad.Owner;
        else
            ent.Comp.ObjectivesSquad = null;

        if (TryResolveComputerMap(ent, out var map))
            UpdateMapData(ent, map.Comp);
        UpdateTacticalMapComputerState(ent);
    }

    private void OnComputerBUIClosed(Entity<TacticalMapComputerComponent> ent, ref BoundUIClosedEvent args)
    {
        // consoles can have multiple concurrent viewers, only stop updating once the last one leaves.
        if (!_ui.IsUiOpen(ent.Owner, TacticalMapComputerUi.Key))
            RemCompDeferred<ActiveTacticalMapComputerComponent>(ent);
    }

    private void OnSquadObjectivesChanged(ref SquadObjectivesChangedEvent args)
    {
        // objective text is pushed only to maps that are currently open.
        var squad = args.Squad;
        var users = EntityQueryEnumerator<TacticalMapUserComponent>();
        while (users.MoveNext(out var member, out var user))
        {
            if (!_ui.IsUiOpen(member, TacticalMapUserUi.Key))
                continue;

            if (!_squad.IsInSquad((member, (SquadMemberComponent?) null), squad.Owner))
                continue;

            UpdateTacticalMapState((member, user));
        }

        var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (computers.MoveNext(out var computerId, out var computer))
        {
            if (computer.ObjectivesSquad != squad.Owner)
                continue;

            if (_ui.IsUiOpen(computerId, TacticalMapComputerUi.Key))
                UpdateTacticalMapComputerState((computerId, computer));
        }
    }

    private void OnUserSelectMapMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapSelectMapMsg args)
    {
        if (!TryGetEntity(args.Map, out var mapEntity) ||
            mapEntity == null ||
            !_tacticalMapQuery.TryComp(mapEntity.Value, out var mapComp))
        {
            return;
        }

        ent.Comp.Map = mapEntity.Value;
        Dirty(ent);

        UpdateUserData(ent, mapComp);
        UpdateTacticalMapState(ent);
    }

    private void OnUserSelectLayerMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapSelectLayerMsg args)
    {
        if (!TryGetSelectedLayer(args.LayerId, out var selected))
            return;

        RefreshUserVisibleLayers(ent);
        var options = ent.Comp.LayerOptions;
        var baseLayers = EnsureBaseLayers(ent);
        if (selected != null && !options.Contains(selected.Value))
            return;

        ent.Comp.ActiveLayer = selected;
        ApplyVisibleLayerSelection(ent, options, baseLayers);
        Dirty(ent);

        if (TryResolveUserMap(ent, out var map))
            UpdateUserData(ent, map.Comp);
    }

    private void OnUserSetVisibleLayersMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapSetVisibleLayersMsg args)
    {
        RefreshUserVisibleLayers(ent);
        var options = ent.Comp.LayerOptions;
        var baseLayers = EnsureBaseLayers(ent);
        var selected = new List<EntProtoId<TacticalMapLayerComponent>>();
        foreach (var layerId in args.LayerIds)
        {
            if (_layers.TryGet(layerId, out _) && options.Contains(layerId))
                selected.Add(layerId);
        }

        ent.Comp.VisibleLayers = selected;
        ApplyVisibleLayerSelection(ent, options, baseLayers);
        if (TryResolveUserMap(ent, out var map))
            UpdateUserData(ent, map.Comp);
    }

    private void OnComputerSelectMapMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapSelectMapMsg args)
    {
        if (!TryGetEntity(args.Map, out var mapEntity) ||
            mapEntity == null ||
            !_tacticalMapQuery.TryComp(mapEntity.Value, out var mapComp))
        {
            return;
        }

        ent.Comp.Map = mapEntity.Value;
        Dirty(ent);

        UpdateMapData((ent, ent), mapComp);
        UpdateTacticalMapComputerState(ent);
    }

    private void OnComputerSelectLayerMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapSelectLayerMsg args)
    {
        if (!TryGetSelectedLayer(args.LayerId, out var selected))
            return;

        RefreshComputerVisibleLayers(ent);
        var options = ent.Comp.LayerOptions;
        var baseLayers = EnsureBaseLayers(ent);
        if (selected != null && !options.Contains(selected.Value))
            return;

        ent.Comp.ActiveLayer = selected;
        ApplyVisibleLayerSelection(ent, options, baseLayers);
        Dirty(ent);

        if (TryResolveComputerMap(ent, out var map))
            UpdateMapData(ent, map.Comp);
    }

    private void OnComputerSetVisibleLayersMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapSetVisibleLayersMsg args)
    {
        RefreshComputerVisibleLayers(ent);
        var options = ent.Comp.LayerOptions;
        var baseLayers = EnsureBaseLayers(ent);
        var selected = new List<EntProtoId<TacticalMapLayerComponent>>();
        foreach (var layerId in args.LayerIds)
        {
            if (_layers.TryGet(layerId, out _) && options.Contains(layerId))
                selected.Add(layerId);
        }

        ent.Comp.VisibleLayers = selected;
        ApplyVisibleLayerSelection(ent, options, baseLayers);
        if (TryResolveComputerMap(ent, out var map))
            UpdateMapData(ent, map.Comp);
    }

    public override void OpenComputerMap(Entity<TacticalMapComputerComponent?> computer, EntityUid user)
    {
        if (!Resolve(computer, ref computer.Comp, false))
            return;

        if (_squad.TryGetMemberSquad(user, out var squad))
            computer.Comp.ObjectivesSquad = squad.Owner;
        else
            computer.Comp.ObjectivesSquad = null;

        _ui.TryOpenUi(computer.Owner, TacticalMapComputerUi.Key, user);
        UpdateMapData((computer, computer.Comp));
        UpdateTacticalMapComputerState((computer.Owner, computer.Comp));
    }

    public override void SetComputerDrawLayerFromSquad(EntityUid computer, EntityUid squad)
    {
        if (HasComp<MarineCommunicationsComputerComponent>(computer))
            return;

        if (!TryComp(computer, out TacticalMapComputerComponent? mapComp))
            return;

        // overwatch forces the selected squad layer visible and active.
        if (!TryGetSquadLayer(squad, out var layer))
            return;
        if (!mapComp.LayerOptions.Contains(layer))
            mapComp.LayerOptions.Add(layer);

        if (!mapComp.VisibleLayers.Contains(layer))
            mapComp.VisibleLayers.Add(layer);

        mapComp.ActiveLayer = layer;
        Dirty(computer, mapComp);

        ApplyVisibleLayerSelection((computer, mapComp), mapComp.LayerOptions, EnsureBaseLayers((computer, mapComp)));

        if (TryResolveComputerMap((computer, mapComp), out var map))
            UpdateMapData((computer, mapComp), map.Comp);
    }
}
