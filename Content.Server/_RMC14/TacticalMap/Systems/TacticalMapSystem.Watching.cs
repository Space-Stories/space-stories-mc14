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
    private void OnOverwatchBlipMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapOverwatchBlipMsg args)
    {
        if (!TryComp(ent.Owner, out OverwatchConsoleComponent? console))
        {
            _popup.PopupCursor(Loc.GetString("rmc-tactical-map-no-overwatch"), args.Actor, PopupType.MediumCaution);
            return;
        }

        if (args.Target == NetEntity.Invalid || !TryGetEntity(args.Target, out var target))
        {
            _popup.PopupCursor(Loc.GetString("rmc-tactical-map-invalid-overwatch-target"), args.Actor, PopupType.MediumCaution);
            return;
        }

        var bypassSquadCheck = HasComp<MarineCommunicationsComputerComponent>(ent.Owner);
        if (!bypassSquadCheck)
        {
            if (console.Squad is not { } squadNet ||
                !TryGetEntity(squadNet, out var squadEnt) ||
                !TryComp(squadEnt, out SquadTeamComponent? squadComp))
            {
                _popup.PopupCursor(Loc.GetString("rmc-tactical-map-no-squad"), args.Actor, PopupType.MediumCaution);
                return;
            }

            if (!_squad.IsInSquad((target.Value, (SquadMemberComponent?) null), squadEnt.Value))
            {
                _popup.PopupCursor(Loc.GetString("rmc-tactical-map-other-squad"), args.Actor, PopupType.MediumCaution);
                return;
            }
        }

        if (!console.ShowHidden && _overwatch.IsHidden((ent.Owner, console), args.Target))
        {
            _popup.PopupCursor(Loc.GetString("rmc-tactical-map-hidden-marine"), args.Actor, PopupType.MediumCaution);
            return;
        }

        if (!_overwatch.TryWatchTarget((ent.Owner, console), args.Actor, target.Value))
        {
            return;
        }
    }

    private void OnUserXenoWatchBlipMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapXenoWatchBlipMsg args)
    {
        if (!ent.Comp.LiveUpdate)
        {
            _popup.PopupCursor(Loc.GetString("rmc-tactical-map-not-live"), args.Actor, PopupType.MediumCaution);
            return;
        }

        if (args.Target == NetEntity.Invalid || !TryGetEntity(args.Target, out var target))
        {
            _popup.PopupCursor(Loc.GetString("rmc-tactical-map-invalid-watch-target"), args.Actor, PopupType.MediumCaution);
            return;
        }

        if (!HasComp<XenoComponent>(args.Actor))
        {
            _popup.PopupCursor(Loc.GetString("rmc-tactical-map-only-xenos"), args.Actor, PopupType.MediumCaution);
            return;
        }

        if (!ent.Comp.Blips.ContainsKey(args.Target) || _mobState.IsDead(target.Value))
            return;

        if (!HasComp<XenoComponent>(target.Value))
        {
            _popup.PopupCursor(Loc.GetString("rmc-tactical-map-not-xeno"), args.Actor, PopupType.MediumCaution);
            return;
        }

        TryComp(args.Actor, out HiveMemberComponent? watcherHive);
        TryComp(args.Actor, out ActorComponent? watcherActor);
        TryComp(args.Actor, out EyeComponent? watcherEye);
        TryComp(target.Value, out HiveMemberComponent? targetHive);

        _xenoWatch.Watch((args.Actor, watcherHive, watcherActor, watcherEye), (target.Value, targetHive));
    }

    private void OnUserQueenEyeMoveMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapQueenEyeMoveMsg args)
    {
        HandleQueenEyeMove(ent, args.Actor, args.Position);
    }

    private void OnComputerQueenEyeMoveMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapQueenEyeMoveMsg args)
    {
        var actor = args.Actor;
        if (!HasComp<GhostComponent>(actor))
            return;

        if (!TryResolveComputerMap(ent, out var map) ||
            !_mapGridQuery.TryComp(map.Owner, out var grid))
        {
            return;
        }

        TeleportGhostToMapPosition(actor, map.Owner, grid.TileSize, args.Position);
    }

    private void HandleQueenEyeMove(Entity<TacticalMapUserComponent> user, EntityUid actor, Vector2i position)
    {
        if (HasComp<GhostComponent>(actor))
        {
            if (!TryResolveUserMap(user, out var ghostMap) ||
                !_mapGridQuery.TryComp(ghostMap.Owner, out var ghostGrid))
            {
                return;
            }

            TeleportGhostToMapPosition(actor, ghostMap.Owner, ghostGrid.TileSize, position);
            return;
        }

        if (!TryComp<QueenEyeActionComponent>(actor, out var queenEyeComp) ||
            queenEyeComp.Eye == null)
            return;

        var eye = queenEyeComp.Eye.Value;

        if (!TryResolveUserMap(user, out var map) ||
            !TryComp<MapGridComponent>(map.Owner, out var grid))
            return;

        // queen eye moves within the selected tacmap grid.
        var queenTransform = Transform(actor);
        var eyeTransform = Transform(eye);
        var mapTransform = Transform(map.Owner);

        if (queenTransform.MapID != mapTransform.MapID)
            return;

        var tileCoords = new Vector2(position.X, position.Y);
        var targetCoords = new EntityCoordinates(map.Owner, tileCoords * grid.TileSize);

        if (!_weeds.IsOnWeeds((map.Owner, grid), targetCoords))
        {
            _popup.PopupCursor(Loc.GetString("rmc-xeno-queen-eye-no-weeds"), actor, PopupType.MediumCaution);
            return;
        }

        var worldPos = _transform.ToMapCoordinates(targetCoords);

        _transform.SetWorldPosition(eye, worldPos.Position);
    }

    private void TeleportGhostToMapPosition(EntityUid actor, EntityUid mapUid, float tileSize, Vector2i position)
    {
        var tileCoords = new Vector2(position.X, position.Y);
        var coords = new EntityCoordinates(mapUid, tileCoords * tileSize);
        _transform.SetCoordinates(actor, coords);
        _transform.AttachToGridOrMap(actor);

        if (TryComp(actor, out PhysicsComponent? physics))
            _physics.SetLinearVelocity(actor, Vector2.Zero, body: physics);
    }
}
