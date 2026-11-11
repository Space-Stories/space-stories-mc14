using System;
using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Marines;
using Content.Server._RMC14.Overwatch;
using Content.Server._RMC14.Xenonids.Watch;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Areas;
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
using Robust.Shared.Maths;
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
    private bool TryStartUserUpdateCooldown(Entity<TacticalMapUserComponent> ent)
    {
        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return false;

        // user edits share one cooldown.
        ent.Comp.LastAnnounceAt = time;
        ent.Comp.NextAnnounceAt = time + _announceCooldown;
        Dirty(ent);
        return true;
    }

    private void OnUserUpdateCanvasMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapUpdateCanvasMsg args)
    {
        var user = args.Actor;
        if (!ent.Comp.CanDraw)
            return;

        if (!TryResolveUserMap(ent, out var map))
            return;

        RefreshUserVisibleLayers(ent);
        var lines = args.Lines;
        if (lines.Count > LineLimit)
            lines = lines[..LineLimit];

        var labels = args.Labels;

        if (!TryGetDrawLayer(ent.Comp.VisibleLayers, ent.Comp.ActiveLayer, out var layer))
            return;

        if (LayerAllowsBlipUpdates(layer) && !TryStartUserUpdateCooldown(ent))
            return;

        // non-marines refresh blip snapshots when drawing.
        var updateBlips = LayerAllowsBlipUpdates(layer) && !HasComp<MarineComponent>(user);
        if (args.CoordinateGrid is { } grid && HasComp<XenoComponent>(ent.Owner) && layer == GlobalXenoLayer)
            UpdateCoordinateGrid(map, GlobalXenoLayer, grid);
        UpdateCanvas(map, lines, labels, layer, user, updateBlips, args.UpdateId, ent.Comp.Sound);
    }

    private void OnComputerUpdateCanvasMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapUpdateCanvasMsg args)
    {
        var user = args.Actor;
        var isGroundside = HasComp<MarineCommunicationsComputerComponent>(ent.Owner);
        var isOverwatch = HasComp<OverwatchConsoleComponent>(ent.Owner);
        if (!isGroundside && !isOverwatch)
            return;

        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
            return;

        if (!TryResolveComputerMap(ent, out var map))
            return;

        RefreshComputerVisibleLayers(ent);
        var lines = args.Lines;
        if (lines.Count > LineLimit)
            lines = lines[..LineLimit];

        var labels = args.Labels;

        if (!TryGetDrawLayer(ent.Comp.VisibleLayers, ent.Comp.ActiveLayer, out var layer))
            return;

        // only groundside computers refresh shared blip snapshots.
        var updateBlips = isGroundside && LayerAllowsBlipUpdates(layer);
        var time = _timing.CurTime;
        if (updateBlips || isGroundside && args.CoordinateGrid != null)
        {
            // Groundside updates share one command-wide cooldown.
            if (time < ent.Comp.NextAnnounceAt)
                return;

            var nextAnnounce = time + _announceCooldown;
            ent.Comp.NextAnnounceAt = nextAnnounce;
            Dirty(ent);

            var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
            while (computers.MoveNext(out var uid, out var computer))
            {
                computer.LastAnnounceAt = time;
                computer.NextAnnounceAt = nextAnnounce;
                Dirty(uid, computer);
            }
        }
        if (isGroundside && args.CoordinateGrid is { } grid)
            UpdateCoordinateGrid(map, GlobalMarineLayer, grid);
        UpdateCanvas(map, lines, labels, layer, user, updateBlips, args.UpdateId);
    }

    private void UpdateCoordinateGrid(Entity<TacticalMapComponent> map,
        EntProtoId<TacticalMapLayerComponent> layer, TacticalMapCoordinateGrid grid)
    {
        if (!TryComp<AreaGridComponent>(map.Owner, out var areas) ||
            !AreaSystem.TryGetTacticalMapBounds(areas, out var minimum, out var maximum))
        {
            return;
        }

        EnsureLayer(map.Comp, layer).CoordinateGrid = grid.Clamped(maximum - minimum + Vector2i.One);
        map.Comp.MapDirty = true;
    }

    private void UpdateCanvas(Entity<TacticalMapComponent> map, List<TacticalMapLine> lines,
        Dictionary<Vector2i, TacticalMapLabelData> labels, EntProtoId<TacticalMapLayerComponent> layer,
        EntityUid user, bool updateBlips, Guid updateId, SoundSpecifier? sound = null)
    {
        SanitizeCanvas(map, ref lines, ref labels);
        var layerData = EnsureLayer(map.Comp, layer);
        map.Comp.MapDirty = true;

        layerData.Lines = lines;
        layerData.Labels = new Dictionary<Vector2i, TacticalMapLabelData>(labels);
        layerData.LastCanvasUpdate = updateId;
        if (updateBlips)
        {
            // store the latest seen positions for non-live viewers.
            layerData.LastUpdateBlips = layerData.Blips.ToDictionary();
            layerData.LastUpdateBlipsAt = _timing.CurTime;

            var snapshotLayers = ApplyLayerVisibilityRules(user, new[] { layer });
            foreach (var extraLayer in snapshotLayers)
            {
                if (extraLayer == layer)
                    continue;

                if (!map.Comp.Layers.TryGetValue(extraLayer, out var extraLayerData))
                    continue;

                foreach (var blip in extraLayerData.Blips)
                {
                    layerData.LastUpdateBlips.TryAdd(blip.Key, blip.Value);
                }
            }
        }

        AnnounceLayerUpdate(layer, user, map.Owner, sound);

        var ev = new TacticalMapUpdatedEvent(lines.ToList(), user);
        RaiseLocalEvent(ref ev);
    }

    private void AnnounceLayerUpdate(EntProtoId<TacticalMapLayerComponent> layer, EntityUid user, EntityUid mapOwner, SoundSpecifier? sound)
    {
        if (_layers.TryGet(layer, out var proto) && !string.IsNullOrWhiteSpace(proto.UpdateAnnouncement))
        {
            switch (proto.AnnouncementTarget)
            {
                case TacticalMapLayerAnnouncementTarget.Marines:
                    _marineAnnounce.AnnounceARESStaging(user, Loc.GetString(proto.UpdateAnnouncement), sound);
                    break;
                case TacticalMapLayerAnnouncementTarget.Xenos:
                    _xenoAnnounce.AnnounceSameHive(user, Loc.GetString(proto.UpdateAnnouncement), sound);
                    break;
            }
        }

        _adminLog.Add(LogType.RMCTacticalMapUpdated,
            $"{ToPrettyString(user)} updated the {GetLayerLogName(layer)} tactical map for {ToPrettyString(mapOwner)}");
    }
}
