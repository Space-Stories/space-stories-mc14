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
    private void AddAlwaysVisibleBlips(
        IReadOnlySet<EntProtoId<TacticalMapLayerComponent>> layers,
        TacticalMapComponent map,
        Dictionary<int, TacticalMapBlip> blips)
    {
        if (layers.Count == 0)
            return;

        // always-visible entities borrow a blip from one of their visible layers.
        var alwaysVisible = EntityQueryEnumerator<TacticalMapAlwaysVisibleComponent>();
        while (alwaysVisible.MoveNext(out var uid, out var comp))
        {
            if (blips.ContainsKey(uid.Id))
                continue;

            var candidateLayers = new List<EntProtoId<TacticalMapLayerComponent>>();
            foreach (var layer in comp.VisibleLayers)
            {
                if (layers.Contains(layer) && LayerAllowsBlipStorage(layer))
                    candidateLayers.Add(layer);
            }

            if (candidateLayers.Count == 0)
                continue;

            if (TryFindBlipInLayers(uid.Id, map, candidateLayers, out var layerBlip))
            {
                blips[uid.Id] = layerBlip;
                continue;
            }

            var fallback = FindBlipInMap(uid.Id, map);
            if (fallback != null)
                blips[uid.Id] = fallback.Value;
        }
    }

    private void AddRangedSensorTowerBlips(
        TacticalMapComponent map,
        IReadOnlySet<EntProtoId<TacticalMapLayerComponent>> visibleLayers,
        Dictionary<int, TacticalMapBlip> blips,
        bool useLastUpdateBlips)
    {
        // ranged towers reveal nearby blips without exposing the whole source layer.
        if (!TryGetTacticalMapOwner(map, out var mapId) ||
            !_mapGridQuery.TryComp(mapId, out var grid))
        {
            return;
        }

        var towers = EntityQueryEnumerator<SensorTowerComponent, TransformComponent>();
        while (towers.MoveNext(out var towerId, out var tower, out var xform))
        {
            if (tower.State != SensorTowerState.On ||
                tower.RevealRange <= 0 ||
                tower.RevealLayers.Count == 0 ||
                !CanSensorTowerReveal(tower, visibleLayers) ||
                xform.GridUid != mapId ||
                !_transform.TryGetGridTilePosition((towerId, xform), out var towerPosition, grid))
            {
                continue;
            }

            var rangeSquared = tower.RevealRange * tower.RevealRange;
            foreach (var layer in tower.RevealLayers)
            {
                if (!LayerAllowsBlipStorage(layer) ||
                    !map.Layers.TryGetValue(layer, out var layerData))
                {
                    continue;
                }

                var sourceBlips = useLastUpdateBlips ? layerData.LastUpdateBlips : layerData.Blips;
                foreach (var (id, blip) in sourceBlips)
                {
                    if (blips.ContainsKey(id))
                        continue;

                    var delta = blip.Indices - towerPosition;
                    if (delta.X * delta.X + delta.Y * delta.Y <= rangeSquared)
                        blips[id] = blip;
                }
            }
        }
    }

    private void AddVehicleSensorRevealBlips(
        TacticalMapComponent map,
        Dictionary<int, TacticalMapBlip> blips,
        bool useLastUpdateBlips)
    {
        // vehicle-mounted sensors reveal nearby xeno blips without exposing the whole layer.
        if (!TryGetTacticalMapOwner(map, out var mapId) ||
            !map.Layers.TryGetValue(GlobalXenoLayer, out var xenoLayer))
        {
            return;
        }

        var rangeEv = new TacticalMapXenoRevealRangeEvent();
        RaiseLocalEvent(ref rangeEv);
        if (rangeEv.Sources.Count == 0)
            return;

        var sourceBlips = useLastUpdateBlips ? xenoLayer.LastUpdateBlips : xenoLayer.Blips;
        foreach (var (id, blip) in sourceBlips)
        {
            if (blips.ContainsKey(id))
                continue;

            foreach (var source in rangeEv.Sources)
            {
                if (source.Grid != mapId)
                    continue;

                var delta = blip.Indices - source.Indices;
                if (delta.X * delta.X + delta.Y * delta.Y > source.Range * source.Range)
                    continue;

                blips[id] = blip;
                break;
            }
        }
    }

    private bool CanSensorTowerReveal(
        SensorTowerComponent tower,
        IReadOnlySet<EntProtoId<TacticalMapLayerComponent>> visibleLayers)
    {
        if (tower.RevealForLayers.Count == 0)
            return true;

        foreach (var layer in tower.RevealForLayers)
        {
            if (visibleLayers.Contains(layer))
                return true;
        }

        return false;
    }

    private bool TryGetTacticalMapOwner(TacticalMapComponent map, out EntityUid owner)
    {
        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var uid, out var mapComp))
        {
            if (ReferenceEquals(mapComp, map))
            {
                owner = uid;
                return true;
            }
        }

        owner = default;
        return false;
    }

    private void AddMarineBlipsFromVisibleSquads(
        EntityUid viewer,
        TacticalMapComponent map,
        IReadOnlyList<EntProtoId<TacticalMapLayerComponent>> visibleLayers,
        Dictionary<int, TacticalMapBlip> blips,
        bool liveUpdate)
    {
        // marine users merge selected squad layers into their blip view.
        if (!_squad.TryGetMemberSquad(viewer, out _))
            return;

        var squadLayers = new HashSet<EntProtoId<TacticalMapLayerComponent>>();
        foreach (var layer in visibleLayers)
        {
            if (_layers.TryGet(layer, out var proto) &&
                proto.Kind == TacticalMapLayerKind.Squad)
            {
                squadLayers.Add(layer);
            }
        }

        if (squadLayers.Count == 0)
            return;

        foreach (var layer in squadLayers)
        {
            if (!map.Layers.TryGetValue(layer, out var layerData))
                continue;

            var sourceBlips = liveUpdate ? layerData.Blips : layerData.LastUpdateBlips;
            foreach (var (id, blip) in sourceBlips)
            {
                blips.TryAdd(id, blip);
            }
        }
    }

}
