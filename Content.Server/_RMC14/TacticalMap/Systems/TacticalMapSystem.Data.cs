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
    private void UpdateTacticalMapState(Entity<TacticalMapUserComponent> ent)
    {
        TryResolveUserMap(ent, out _);

        ent.Comp.Objectives = BuildObjectiveStateForUser(ent.Owner);
        Dirty(ent);
    }

    private void UpdateTacticalMapComputerState(Entity<TacticalMapComputerComponent> computer)
    {
        TryResolveComputerMap(computer, out _);

        computer.Comp.Objectives = BuildObjectiveStateForComputer(computer);
        Dirty(computer);
    }

    private Dictionary<SquadObjectiveType, string> BuildObjectiveState(SquadTeamComponent squad)
    {
        var objectives = new Dictionary<SquadObjectiveType, string>();
        foreach (var type in Enum.GetValues<SquadObjectiveType>())
        {
            objectives[type] = string.Empty;
        }

        foreach (var (type, objective) in squad.Objectives)
        {
            objectives[type] = objective ?? string.Empty;
        }

        return objectives;
    }

    private Dictionary<SquadObjectiveType, string> BuildObjectiveStateForUser(EntityUid viewer)
    {
        if (_squad.TryGetMemberSquad(viewer, out var squad))
            return BuildObjectiveState(squad.Comp);

        return new Dictionary<SquadObjectiveType, string>();
    }

    private Dictionary<SquadObjectiveType, string> BuildObjectiveStateForComputer(Entity<TacticalMapComputerComponent> computer)
    {
        if (computer.Comp.ObjectivesSquad != null &&
            TryComp(computer.Comp.ObjectivesSquad.Value, out SquadTeamComponent? squad))
        {
            return BuildObjectiveState(squad);
        }

        return new Dictionary<SquadObjectiveType, string>();
    }

    private TacticalMapLayerData EnsureLayer(TacticalMapComponent map, EntProtoId<TacticalMapLayerComponent> layer)
    {
        if (!map.Layers.TryGetValue(layer, out var data))
        {
            data = new TacticalMapLayerData();
            map.Layers[layer] = data;
        }

        return data;
    }

    private string GetLayerLogName(EntProtoId<TacticalMapLayerComponent> layer)
    {
        if (_layers.TryGet(layer, out var proto) && !string.IsNullOrWhiteSpace(proto.LogName))
            return proto.LogName;

        return layer.Id;
    }

    private Dictionary<int, TacticalMapBlip> BuildBlipsFromLayers(
        TacticalMapComponent map,
        IReadOnlyCollection<EntProtoId<TacticalMapLayerComponent>> layers,
        bool useLastUpdateBlips)
    {
        // non-live viewers read the last announced snapshot instead of live positions.
        var blips = new Dictionary<int, TacticalMapBlip>();
        foreach (var layer in layers)
        {
            if (!LayerAllowsBlipStorage(layer))
                continue;

            if (!map.Layers.TryGetValue(layer, out var layerData))
                continue;

            var sourceBlips = useLastUpdateBlips ? layerData.LastUpdateBlips : layerData.Blips;
            foreach (var (id, blip) in sourceBlips)
            {
                blips.TryAdd(id, blip);
            }
        }

        return blips;
    }

    private void BuildLineAndLabelState(
        TacticalMapComponent map,
        IReadOnlyList<EntProtoId<TacticalMapLayerComponent>> layers,
        out List<TacticalMapLine> lines,
        out Dictionary<Vector2i, TacticalMapLabelData> labels)
    {
        // lines stack, labels are replaced by later visible layers at the same tile.
        lines = new List<TacticalMapLine>();
        labels = new Dictionary<Vector2i, TacticalMapLabelData>();

        foreach (var layer in layers)
        {
            if (!map.Layers.TryGetValue(layer, out var layerData))
                continue;

            lines.AddRange(layerData.Lines);
            foreach (var (pos, label) in layerData.Labels)
            {
                labels[pos] = label;
            }
        }
    }

    private bool TryFindBlipInLayers(
        int entityId,
        TacticalMapComponent map,
        IReadOnlyCollection<EntProtoId<TacticalMapLayerComponent>> layers,
        out TacticalMapBlip blip)
    {
        if (layers.Count == 0)
        {
            blip = default;
            return false;
        }

        var orderedLayers = layers.ToList();
        orderedLayers.Sort(CompareLayerOrder);

        foreach (var layer in orderedLayers)
        {
            if (!map.Layers.TryGetValue(layer, out var layerData))
                continue;

            if (layerData.Blips.TryGetValue(entityId, out blip))
                return true;
        }

        blip = default;
        return false;
    }

    private static TacticalMapCoordinateGrid? GetCoordinateGrid(TacticalMapComponent map,
        IReadOnlyList<EntProtoId<TacticalMapLayerComponent>> layers)
    {
        var layer = layers.Contains(GlobalMarineLayer) ? GlobalMarineLayer : GlobalXenoLayer;
        return layers.Contains(layer) && map.Layers.TryGetValue(layer, out var data)
            ? data.CoordinateGrid
            : null;
    }

    public override void UpdateUserData(Entity<TacticalMapUserComponent> user, TacticalMapComponent map)
    {
        if (TerminatingOrDeleted(user))
            return;

        // user state is filtered before being networked to the client.
        var lines = EnsureComp<TacticalMapLinesComponent>(user);
        var labels = EnsureComp<TacticalMapLabelsComponent>(user);

        RefreshUserVisibleLayers(user);
        var visibleLayers = GetEffectiveVisibleLayers(user.Comp.VisibleLayers);
        var blipLayers = ApplyLayerVisibilityRules(user.Owner, visibleLayers);
        var blips = BuildBlipsFromLayers(map, blipLayers, useLastUpdateBlips: !user.Comp.LiveUpdate);
        AddAlwaysVisibleBlips(blipLayers, map, blips);
        AddRangedSensorTowerBlips(map, blipLayers, blips, useLastUpdateBlips: !user.Comp.LiveUpdate);
        AddVehicleSensorRevealBlips(map, blips, useLastUpdateBlips: !user.Comp.LiveUpdate);

        AddSelfBlip(user.Owner, map, blips);
        FilterXenoBlipsForHive(user.Owner, blips);

        if (HasComp<MarineComponent>(user.Owner))
        {
            AddMarineBlipsFromVisibleSquads(user.Owner, map, visibleLayers, blips, user.Comp.LiveUpdate);
        }

        user.Comp.Blips = ToNetworkBlips(blips);
        // clients fade stale snapshots from the layer update time.
        user.Comp.LastBlipUpdateAt = GetLastBlipUpdateAt(map, blipLayers);

        BuildLineAndLabelState(map, visibleLayers, out var combinedLines, out var combinedLabels);
        lines.Lines = combinedLines;
        lines.CoordinateGrid = GetCoordinateGrid(map, visibleLayers);
        labels.Labels = combinedLabels;

        var activeLines = EnsureComp<TacticalMapActiveLayerLinesComponent>(user);
        var activeLabels = EnsureComp<TacticalMapActiveLayerLabelsComponent>(user);
        var activeLayers = GetActiveLayers(visibleLayers, user.Comp.ActiveLayer);
        BuildLineAndLabelState(map, activeLayers, out var activeLayerLines, out var activeLayerLabels);
        activeLines.Lines = activeLayerLines;
        activeLabels.Labels = activeLayerLabels;
        activeLines.UpdateId = GetCanvasUpdateId(map, activeLayers);
        activeLabels.UpdateId = activeLines.UpdateId;

        Dirty(user);
        Dirty(user, lines);
        Dirty(user, labels);
        Dirty(user, activeLines);
        Dirty(user, activeLabels);
    }

    private void FilterXenoBlipsForHive(EntityUid viewer, Dictionary<int, TacticalMapBlip> blips)
    {
        if (!HasComp<XenoComponent>(viewer))
            return;

        TryComp(viewer, out HiveMemberComponent? viewerHive);
        var viewerHiveId = viewerHive?.Hive;

        var toRemove = new List<int>();
        foreach (var (entityId, _) in blips)
        {
            if (entityId == viewer.Id)
                continue;

            var target = new EntityUid(entityId);
            if (TerminatingOrDeleted(target))
                continue;

            if (!HasComp<XenoComponent>(target))
                continue;

            if (!TryComp(target, out HiveMemberComponent? targetHive) ||
                viewerHiveId == null ||
                targetHive.Hive == null ||
                targetHive.Hive != viewerHiveId)
            {
                toRemove.Add(entityId);
            }
        }

        foreach (var entityId in toRemove)
        {
            blips.Remove(entityId);
        }
    }

    private TimeSpan GetLastBlipUpdateAt(TacticalMapComponent map, IReadOnlySet<EntProtoId<TacticalMapLayerComponent>> layers)
    {
        var lastUpdate = TimeSpan.Zero;
        foreach (var layer in layers)
        {
            if (!map.Layers.TryGetValue(layer, out var layerData))
                continue;

            if (layerData.LastUpdateBlipsAt > lastUpdate)
                lastUpdate = layerData.LastUpdateBlipsAt;
        }

        return lastUpdate;
    }

    private TacticalMapBlip? FindBlipInMap(int entityId, TacticalMapComponent map)
    {
        if (TryFindBlipInLayers(entityId, map, map.Layers.Keys, out var blip))
            return blip;

        return null;
    }

    private void AddSelfBlip(EntityUid user, TacticalMapComponent map, Dictionary<int, TacticalMapBlip> blips)
    {
        if (blips.ContainsKey(user.Id))
            return;

        var blip = FindBlipInMap(user.Id, map);
        if (blip == null)
            return;

        blips[user.Id] = blip.Value;
    }

    protected override void UpdateMapData(Entity<TacticalMapComputerComponent> computer, TacticalMapComponent map)
    {
        // computer maps always receive live blips for their allowed layers.
        RefreshComputerVisibleLayers(computer);
        var visibleLayers = GetEffectiveVisibleLayers(computer.Comp.VisibleLayers);
        var blipLayers = ApplyLayerVisibilityRules(computer.Owner, visibleLayers);

        var blips = BuildBlipsFromLayers(map, blipLayers, useLastUpdateBlips: false);
        AddRangedSensorTowerBlips(map, blipLayers, blips, useLastUpdateBlips: false);
        AddVehicleSensorRevealBlips(map, blips, useLastUpdateBlips: false);
        computer.Comp.Blips = ToNetworkBlips(blips);
        Dirty(computer);

        var lines = EnsureComp<TacticalMapLinesComponent>(computer);
        var labels = EnsureComp<TacticalMapLabelsComponent>(computer);

        BuildLineAndLabelState(map, visibleLayers, out var combinedLines, out var combinedLabels);
        lines.Lines = combinedLines;
        lines.CoordinateGrid = GetCoordinateGrid(map, visibleLayers);
        labels.Labels = combinedLabels;

        var activeLines = EnsureComp<TacticalMapActiveLayerLinesComponent>(computer);
        var activeLabels = EnsureComp<TacticalMapActiveLayerLabelsComponent>(computer);
        var activeLayers = GetActiveLayers(visibleLayers, computer.Comp.ActiveLayer);
        BuildLineAndLabelState(map, activeLayers, out var activeLayerLines, out var activeLayerLabels);
        activeLines.Lines = activeLayerLines;
        activeLabels.Labels = activeLayerLabels;
        activeLines.UpdateId = GetCanvasUpdateId(map, activeLayers);
        activeLabels.UpdateId = activeLines.UpdateId;

        Dirty(computer, lines);
        Dirty(computer, labels);
        Dirty(computer, activeLines);
        Dirty(computer, activeLabels);
    }

    private static Guid GetCanvasUpdateId(TacticalMapComponent map,
        IReadOnlyList<EntProtoId<TacticalMapLayerComponent>> layers)
    {
        return layers.Count == 1 && map.Layers.TryGetValue(layers[0], out var layer)
            ? layer.LastCanvasUpdate
            : Guid.Empty;
    }
}
