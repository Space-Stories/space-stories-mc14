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
    private readonly HashSet<EntProtoId<TacticalMapLayerComponent>> _layerAccessBuffer = new();

    private void OnTrackedMapInit(Entity<TacticalMapTrackedComponent> ent, ref MapInitEvent args)
    {
        _toInit.Add(ent);
        if (TryComp(ent, out ActiveTacticalMapTrackedComponent? active))
            _toUpdate.Add((ent, active));
    }

    private void OnTrackedMobStateChanged(Entity<TacticalMapTrackedComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateActiveTracking(ent, args.NewMobState);
    }

    private void OnTrackedChanged<T>(Entity<TacticalMapTrackedComponent> ent, ref T args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        UpdateActiveTracking(ent);
    }

    private void OnActiveRemove<T>(Entity<ActiveTacticalMapTrackedComponent> ent, ref T args)
    {
        BreakTracking(ent);
    }

    private void OnActiveTrackedMove(Entity<ActiveTacticalMapTrackedComponent> ent, ref MoveEvent args)
    {
        _toUpdate.Add(ent);
    }

    private void OnActiveTrackedRoleAdded(Entity<ActiveTacticalMapTrackedComponent> ent, ref RoleAddedEvent args)
    {
        UpdateIcon(ent);
        UpdateTracked(ent);
    }

    private void OnActiveTrackedMindAdded(Entity<ActiveTacticalMapTrackedComponent> ent, ref MindAddedMessage args)
    {
        UpdateIcon(ent);
        UpdateTracked(ent);
    }

    private void OnActiveSquadMemberUpdated(Entity<ActiveTacticalMapTrackedComponent> ent, ref SquadMemberUpdatedEvent args)
    {
        if (_squadTeamQuery.TryComp(args.Squad, out var squad))
        {
            if (squad.MinimapBackground != null)
            {
                ent.Comp.Background = squad.MinimapBackground;
                ent.Comp.Color = Color.White;
            }
            else
                ent.Comp.Color = squad.Color;
        }
        else if (ent.Comp.Background != null)
            UpdateIcon(ent);

        _toUpdate.Add(ent);
    }

    private void OnLayerTrackedSquadMemberUpdated(Entity<TacticalMapLayerTrackedComponent> ent, ref SquadMemberUpdatedEvent args)
    {
        // squad changes move tracked entities between squad-owned blip layers.
        var changed = false;
        var allSquadLayers = GetAllSquadLayers();
        foreach (var layer in allSquadLayers)
        {
            if (ent.Comp.Layers.Remove(layer))
                changed = true;
        }

        if (TryGetSquadLayer(args.Squad, out var squadLayer))
        {
            if (!ent.Comp.Layers.Contains(squadLayer))
            {
                ent.Comp.Layers.Add(squadLayer);
                changed = true;
            }
        }

        if (_squadTeamQuery.TryComp(args.Squad, out var squadTeam) &&
            string.Equals(squadTeam.Group, "FORECON", StringComparison.OrdinalIgnoreCase))
        {
            if (!ent.Comp.Layers.Contains(GlobalMarineLayer))
            {
                ent.Comp.Layers.Add(GlobalMarineLayer);
                changed = true;
            }
        }

        if (changed)
        {
            Dirty(ent);
            if (_activeTacticalMapTrackedQuery.TryComp(ent.Owner, out var active))
                UpdateTracked((ent.Owner, active));
        }
    }

    private void OnActiveMobStateChanged(Entity<ActiveTacticalMapTrackedComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateIcon(ent);
        UpdateTracked(ent);
    }

    private void OnHiveLeaderStatusChanged(Entity<ActiveTacticalMapTrackedComponent> ent, ref HiveLeaderStatusChangedEvent args)
    {
        UpdateIcon(ent);
        UpdateHiveLeader(ent, args.BecameLeader);
        UpdateTracked(ent);
    }

    private void OnMapBlipOverrideMapInit(Entity<MapBlipIconOverrideComponent> ent, ref MapInitEvent args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
        {
            UpdateIcon((ent.Owner, active));
            UpdateTracked((ent.Owner, active));
        }
    }

    private void OnIconChanged(Entity<ActiveTacticalMapTrackedComponent> ent, ref TacticalMapIconChangedEvent args)
    {
        UpdateIcon(ent);
        _toUpdate.Add(ent);
    }

    private void OnRottingMapInit(Entity<RottingComponent> ent, ref MapInitEvent args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void OnRottingRemove(Entity<RottingComponent> ent, ref ComponentRemove args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void OnUnrevivableMapInit(Entity<UnrevivableComponent> ent, ref MapInitEvent args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void OnUnrevivablRemove(Entity<UnrevivableComponent> ent, ref ComponentRemove args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void UpdateActiveTracking(Entity<TacticalMapTrackedComponent> tracked, MobState mobState)
    {
        if (!tracked.Comp.TrackDead && mobState == MobState.Dead)
        {
            RemCompDeferred<ActiveTacticalMapTrackedComponent>(tracked);
            return;
        }

        if (LifeStage(tracked) < EntityLifeStage.MapInitialized)
            return;

        var active = EnsureComp<ActiveTacticalMapTrackedComponent>(tracked);
        var activeEnt = new Entity<ActiveTacticalMapTrackedComponent>(tracked, active);
        UpdateIcon(activeEnt);
        UpdateRotting(activeEnt);
        UpdateColor(activeEnt);
    }

    private void UpdateActiveTracking(Entity<TacticalMapTrackedComponent> tracked)
    {
        var state = _mobStateQuery.CompOrNull(tracked)?.CurrentState ?? MobState.Alive;
        UpdateActiveTracking(tracked, state);
    }

    private void BreakTracking(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        if (!_tacticalMapQuery.TryComp(tracked.Comp.Map, out var tacticalMap))
            return;

        // leaving a map removes stale blips from every layer.
        foreach (var layerData in tacticalMap.Layers.Values)
        {
            layerData.Blips.Remove(tracked.Owner.Id);
        }

        tacticalMap.MapDirty = true;
        tracked.Comp.Map = null;
    }

    private void UpdateIcon(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        SpriteSpecifier.Rsi? mapBlipOverride = null;
        if (TryComp<MapBlipIconOverrideComponent>(tracked, out var mapBlipOverrideComp) && mapBlipOverrideComp.Icon != null)
            mapBlipOverride = mapBlipOverrideComp.Icon;

        if (_tacticalMapIconQuery.TryComp(tracked, out var iconComp))
        {
            tracked.Comp.Icon = mapBlipOverride ?? iconComp.Icon;
            tracked.Comp.Background = iconComp.Background;
            UpdateSquadBackground(tracked);
            return;
        }

        tracked.Comp.Icon = mapBlipOverride;
        UpdateSquadBackground(tracked);
    }

    private void UpdateSquadBackground(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        // squad backgrounds override job backgrounds.
        if (!_squad.TryGetMemberSquad(tracked.Owner, out var squad))
            return;

        tracked.Comp.Background = squad.Comp.MinimapBackground;
        if (TryComp(tracked, out TacticalMapIconComponent? icon))
        {
            icon.Background = tracked.Comp.Background;
            Dirty(tracked, icon);
        }
    }

    private void UpdateRotting(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        tracked.Comp.Undefibbable = _rottingQuery.HasComp(tracked);
    }

    private void UpdateColor(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        if (_squad.TryGetMemberSquad(tracked.Owner, out var squad))
        {
            if (squad.Comp.MinimapBackground == null)
                tracked.Comp.Color = squad.Comp.Color;
            else
            {
                tracked.Comp.Background = squad.Comp.MinimapBackground;
                tracked.Comp.Color = Color.White;
            }
        }
        else
        {
            tracked.Comp.Color = Color.White;
        }

        if (TryComp(tracked, out TacticalMapIconComponent? icon))
        {
            icon.Background = tracked.Comp.Background;
            Dirty(tracked, icon);
        }
    }

    private void UpdateHiveLeader(Entity<ActiveTacticalMapTrackedComponent> tracked, bool isLeader)
    {
        tracked.Comp.HiveLeader = isLeader;
    }


    private void AddLayerAccessLayers(EntityUid user, HashSet<EntProtoId<TacticalMapLayerComponent>> layers)
    {
        if (!_layerAccess.TryGetLayers(user, _layerAccessBuffer))
            return;

        foreach (var layer in _layerAccessBuffer)
        {
            layers.Add(layer);
        }
    }

    private void UpdateTracked(Entity<ActiveTacticalMapTrackedComponent> ent)
    {
        if (!_transformQuery.TryComp(ent.Owner, out var xform) ||
            xform.GridUid is not { } gridId ||
            !_mapGridQuery.TryComp(gridId, out var gridComp) ||
            !_tacticalMapQuery.TryComp(gridId, out var tacticalMap) ||
            !_transform.TryGetGridTilePosition((ent.Owner, xform), out var indices, gridComp))
        {
            BreakTracking(ent);
            return;
        }

        if (ent.Comp.Icon == null)
            UpdateIcon(ent);

        if (ent.Comp.Icon is not { } icon)
        {
            BreakTracking(ent);
            return;
        }

        if (ent.Comp.Map != xform.GridUid)
        {
            BreakTracking(ent);
            ent.Comp.Map = xform.GridUid;
        }

        var status = TacticalMapBlipStatus.Alive;
        if (_mobState.IsDead(ent))
        {
            var stage = _unrevivable.GetUnrevivableStage(ent.Owner, 5);
            if (_rottingQuery.HasComp(ent) || _unrevivable.IsUnrevivable(ent))
                status = TacticalMapBlipStatus.Undefibabble;
            else if (stage <= 1)
                status = TacticalMapBlipStatus.Defibabble;
            else if (stage == 2)
                status = TacticalMapBlipStatus.Defibabble2;
            else if (stage == 3)
                status = TacticalMapBlipStatus.Defibabble3;
            else if (stage == 4)
                status = TacticalMapBlipStatus.Defibabble4;
        }

        var blip = new TacticalMapBlip(indices, icon, ent.Comp.Color, status, ent.Comp.Background, ent.Comp.HiveLeader);
        var updated = false;
        var layerIds = new HashSet<EntProtoId<TacticalMapLayerComponent>>();

        // each tracked entity owns its blip membership across all layers.
        if (_tacticalMapLayerTrackedQuery.TryComp(ent, out var layerTracked) &&
            layerTracked.Layers.Count > 0)
        {
            foreach (var layerId in layerTracked.Layers)
            {
                layerIds.Add(layerId);
            }
        }

        if (HasComp<MarineMapTrackedComponent>(ent))
            layerIds.Add(GlobalMarineLayer);
        if (HasComp<XenoMapTrackedComponent>(ent) || HasComp<XenoStructureMapTrackedComponent>(ent))
            layerIds.Add(GlobalXenoLayer);
        if (TryGetViewerSquadLayer(ent.Owner, out var squadLayer))
            layerIds.Add(squadLayer);

        AddLayerAccessLayers(ent.Owner, layerIds);

        foreach (var (layerId, layerData) in tacticalMap.Layers)
        {
            if (layerIds.Contains(layerId) && LayerAllowsBlipStorage(layerId))
                continue;

            if (layerData.Blips.Remove(ent.Owner.Id))
                updated = true;
        }

        foreach (var layerId in layerIds)
        {
            if (!LayerAllowsBlipStorage(layerId))
                continue;

            var layerData = EnsureLayer(tacticalMap, layerId);
            if (!layerData.Blips.TryGetValue(ent.Owner.Id, out var existing) || !existing.Equals(blip))
            {
                layerData.Blips[ent.Owner.Id] = blip;
                updated = true;
            }
        }

        if (updated)
            tacticalMap.MapDirty = true;
    }
}
