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
using Content.Shared.GameTicking;
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

public sealed partial class TacticalMapSystem : SharedTacticalMapSystem
{
    private static readonly EntProtoId<TacticalMapLayerComponent> GlobalMarineLayer = "RMCTacticalMapLayerMarines";
    private static readonly EntProtoId<TacticalMapLayerComponent> GlobalXenoLayer = "RMCTacticalMapLayerXenos";
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly TacticalMapLayerAccessSystem _layerAccess = default!;
    [Dependency] private readonly TacticalMapLayerSystem _layers = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly OverwatchConsoleSystem _overwatch = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TacticalMapReplaySystem _replay = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly TacMapLiveUpdateSystem _tacMapLiveUpdate = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private readonly SharedXenoWeedsSystem _weeds = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly XenoWatchSystem _xenoWatch = default!;

    private EntityQuery<ActiveTacticalMapTrackedComponent> _activeTacticalMapTrackedQuery;
    private EntityQuery<TacticalMapLayerTrackedComponent> _tacticalMapLayerTrackedQuery;
    private EntityQuery<MapGridComponent> _mapGridQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<RottingComponent> _rottingQuery;
    private EntityQuery<SquadTeamComponent> _squadTeamQuery;
    private EntityQuery<TacticalMapIconComponent> _tacticalMapIconQuery;
    private EntityQuery<TacticalMapComponent> _tacticalMapQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    // blip writes are deferred so bursts of movement collapse into one update.
    private readonly HashSet<Entity<TacticalMapTrackedComponent>> _toInit = new();
    private readonly HashSet<Entity<ActiveTacticalMapTrackedComponent>> _toUpdate = new();
    private TimeSpan _announceCooldown;
    private TimeSpan _mapUpdateEvery;
    private TimeSpan _forceMapUpdateEvery;
    private TimeSpan _nextForceMapUpdate;

    public override void Initialize()
    {
        base.Initialize();

        _activeTacticalMapTrackedQuery = GetEntityQuery<ActiveTacticalMapTrackedComponent>();
        _tacticalMapLayerTrackedQuery = GetEntityQuery<TacticalMapLayerTrackedComponent>();
        _mapGridQuery = GetEntityQuery<MapGridComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _rottingQuery = GetEntityQuery<RottingComponent>();
        _squadTeamQuery = GetEntityQuery<SquadTeamComponent>();
        _tacticalMapIconQuery = GetEntityQuery<TacticalMapIconComponent>();
        _tacticalMapQuery = GetEntityQuery<TacticalMapComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<XenoOvipositorChangedEvent>(OnOvipositorChanged);

        SubscribeLocalEvent<TacticalMapComponent, MapInitEvent>(OnTacticalMapMapInit);

        SubscribeLocalEvent<TacticalMapUserComponent, MapInitEvent>(OnUserMapInit);

        SubscribeLocalEvent<TacticalMapComputerComponent, MapInitEvent>(OnComputerMapInit);
        SubscribeLocalEvent<TacticalMapComputerComponent, BeforeActivatableUIOpenEvent>(OnComputerBeforeUIOpen);

        SubscribeLocalEvent<TacticalMapTrackedComponent, MapInitEvent>(OnTrackedMapInit);
        SubscribeLocalEvent<TacticalMapTrackedComponent, MobStateChangedEvent>(OnTrackedMobStateChanged);
        SubscribeLocalEvent<TacticalMapTrackedComponent, RoleAddedEvent>(OnTrackedChanged);
        SubscribeLocalEvent<TacticalMapTrackedComponent, MindAddedMessage>(OnTrackedChanged);
        SubscribeLocalEvent<TacticalMapTrackedComponent, SquadMemberUpdatedEvent>(OnTrackedChanged);
        SubscribeLocalEvent<TacticalMapUserComponent, SquadMemberUpdatedEvent>(OnUserSquadMemberUpdated);
        SubscribeLocalEvent<TacticalMapUserComponent, SquadMemberRemovedEvent>(OnUserSquadMemberRemoved);
        SubscribeLocalEvent<TacticalMapTrackedComponent, EntParentChangedMessage>(OnTrackedChanged);
        SubscribeLocalEvent<SquadObjectivesChangedEvent>(OnSquadObjectivesChanged);
        SubscribeLocalEvent<TacticalMapLayerTrackedComponent, SquadMemberUpdatedEvent>(OnLayerTrackedSquadMemberUpdated);

        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, ComponentRemove>(OnActiveRemove);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, EntityTerminatingEvent>(OnActiveRemove);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, MoveEvent>(OnActiveTrackedMove);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, RoleAddedEvent>(OnActiveTrackedRoleAdded);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, MindAddedMessage>(OnActiveTrackedMindAdded);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, SquadMemberUpdatedEvent>(OnActiveSquadMemberUpdated);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, MobStateChangedEvent>(OnActiveMobStateChanged);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, HiveLeaderStatusChangedEvent>(OnHiveLeaderStatusChanged);

        SubscribeLocalEvent<MapBlipIconOverrideComponent, MapInitEvent>(OnMapBlipOverrideMapInit);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, TacticalMapIconChangedEvent>(OnIconChanged);


        SubscribeLocalEvent<RottingComponent, MapInitEvent>(OnRottingMapInit);
        SubscribeLocalEvent<RottingComponent, ComponentRemove>(OnRottingRemove);

        SubscribeLocalEvent<UnrevivableComponent, MapInitEvent>(OnUnrevivableMapInit);
        SubscribeLocalEvent<UnrevivableComponent, ComponentRemove>(OnUnrevivablRemove);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<TacticalMapLiveUpdateOnOviComponent, MapInitEvent>(OnLiveUpdateOnOviMapInit);
        SubscribeLocalEvent<TacticalMapLiveUpdateOnOviComponent, MobStateChangedEvent>(OnLiveUpdateOnOviStateChanged);

        Subs.BuiEvents<TacticalMapUserComponent>(TacticalMapUserUi.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUserBUIOpened);
                subs.Event<BoundUIClosedEvent>(OnUserBUIClosed);
                subs.Event<TacticalMapSelectMapMsg>(OnUserSelectMapMsg);
                subs.Event<TacticalMapSelectLayerMsg>(OnUserSelectLayerMsg);
                subs.Event<TacticalMapUpdateCanvasMsg>(OnUserUpdateCanvasMsg);
                subs.Event<TacticalMapQueenEyeMoveMsg>(OnUserQueenEyeMoveMsg);
                subs.Event<TacticalMapXenoWatchBlipMsg>(OnUserXenoWatchBlipMsg);
                subs.Event<TacticalMapSetVisibleLayersMsg>(OnUserSetVisibleLayersMsg);
            });

        Subs.BuiEvents<TacticalMapComputerComponent>(TacticalMapComputerUi.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnComputerBUIOpened);
                subs.Event<BoundUIClosedEvent>(OnComputerBUIClosed);
                subs.Event<TacticalMapSelectMapMsg>(OnComputerSelectMapMsg);
                subs.Event<TacticalMapSelectLayerMsg>(OnComputerSelectLayerMsg);
                subs.Event<TacticalMapUpdateCanvasMsg>(OnComputerUpdateCanvasMsg);
                subs.Event<TacticalMapQueenEyeMoveMsg>(OnComputerQueenEyeMoveMsg);
                subs.Event<TacticalMapOverwatchBlipMsg>(OnOverwatchBlipMsg);
                subs.Event<TacticalMapSetVisibleLayersMsg>(OnComputerSetVisibleLayersMsg);
            });

        Subs.CVar(_config,
            RMCCVars.RMCTacticalMapAnnounceCooldownSeconds,
            v => _announceCooldown = TimeSpan.FromSeconds(v),
            true);

        Subs.CVar(_config,
            RMCCVars.RMCTacticalMapUpdateEverySeconds,
            v => _mapUpdateEvery = TimeSpan.FromSeconds(v),
            true);

        Subs.CVar(_config,
            RMCCVars.RMCTacticalMapForceUpdateEverySeconds,
            v => _forceMapUpdateEvery = TimeSpan.FromSeconds(v),
            true);
    }

    private void OnOvipositorChanged(ref XenoOvipositorChangedEvent ev)
    {
        var users = EntityQueryEnumerator<TacticalMapLiveUpdateOnOviComponent, TacticalMapUserComponent>();
        while (users.MoveNext(out var uid, out var onOvi, out var user))
        {
            if (!onOvi.Enabled)
                continue;

            _tacMapLiveUpdate.RefreshLiveUpdate((uid, user));
            if (TryResolveUserMap((uid, user), out var map))
                map.Comp.MapDirty = true;
        }
    }

    private void OnTacticalMapMapInit(Entity<TacticalMapComponent> ent, ref MapInitEvent args)
    {
        // resolve once and network the result so clients can build their own map list.
        ent.Comp.MapId = ResolveMapId(ent);
        ent.Comp.DisplayName = ResolveMapDisplayName(ent, ent.Comp.MapId);
        Dirty(ent);

        // a new map can become the fallback for unresolved users and computers.
        var tracked = EntityQueryEnumerator<ActiveTacticalMapTrackedComponent, TacticalMapTrackedComponent>();
        while (tracked.MoveNext(out var uid, out var active, out var comp))
        {
            UpdateActiveTracking((uid, comp));
            UpdateTracked((uid, active));
        }

        var users = EntityQueryEnumerator<TacticalMapUserComponent>();
        while (users.MoveNext(out var userId, out var userComp))
        {
            if (userComp.Map == null || !_tacticalMapQuery.HasComp(userComp.Map.Value))
            {
                userComp.Map = ent;
                Dirty(userId, userComp);
            }
        }

        var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (computers.MoveNext(out var computerId, out var computerComp))
        {
            if (computerComp.Map == null || !_tacticalMapQuery.HasComp(computerComp.Map.Value))
            {
                computerComp.Map = ent;
                Dirty(computerId, computerComp);
            }
        }

        var activeUsers = EntityQueryEnumerator<ActiveTacticalMapUserComponent, TacticalMapUserComponent>();
        while (activeUsers.MoveNext(out var userId, out _, out var userComp))
        {
            UpdateTacticalMapState((userId, userComp));
        }

        var activeComputers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (activeComputers.MoveNext(out var computerId, out var computerComp))
        {
            if (_ui.IsUiOpen(computerId, TacticalMapComputerUi.Key))
                UpdateTacticalMapComputerState((computerId, computerComp));
        }
    }

    private void OnUserMapInit(Entity<TacticalMapUserComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);

        TryResolveUserMap(ent, out _);
        _tacMapLiveUpdate.RefreshLiveUpdate(ent);
        RefreshUserVisibleLayers(ent);

        Dirty(ent);
    }

    private void OnComputerMapInit(Entity<TacticalMapComputerComponent> ent, ref MapInitEvent args)
    {
        TryResolveComputerMap(ent, out _);
        RefreshComputerVisibleLayers(ent);

        Dirty(ent);
    }

    private void OnComputerBeforeUIOpen(Entity<TacticalMapComputerComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        if (TryResolveComputerMap(ent, out var map))
            UpdateMapData((ent, ent), map.Comp);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        _nextForceMapUpdate = _timing.CurTime + _forceMapUpdateEvery;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _toInit.Clear();
        _toUpdate.Clear();
        _nextForceMapUpdate = TimeSpan.Zero;
    }

    private void OnLiveUpdateOnOviMapInit(Entity<TacticalMapLiveUpdateOnOviComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Enabled ||
            !TryComp(ent, out TacticalMapUserComponent? user))
        {
            return;
        }

        _tacMapLiveUpdate.RefreshLiveUpdate((ent.Owner, user));
    }

    private void OnLiveUpdateOnOviStateChanged(Entity<TacticalMapLiveUpdateOnOviComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            RemCompDeferred<TacticalMapLiveUpdateOnOviComponent>(ent);
    }

    private void OnUserBUIClosed(Entity<TacticalMapUserComponent> ent, ref BoundUIClosedEvent args)
    {
        RemCompDeferred<ActiveTacticalMapUserComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        try
        {
            foreach (var init in _toInit)
            {
                if (!init.Comp.Running)
                    continue;

                var wasActive = HasComp<ActiveTacticalMapTrackedComponent>(init);
                UpdateActiveTracking(init);

                if (!wasActive && TryComp(init, out ActiveTacticalMapTrackedComponent? active))
                    UpdateTracked((init, active));
            }
        }
        finally
        {
            _toInit.Clear();
        }

        var time = _timing.CurTime;
        if (time > _nextForceMapUpdate)
        {
            // periodic full refresh catches changes without movement events.
            _nextForceMapUpdate = time + _forceMapUpdateEvery;
            var tracked = EntityQueryEnumerator<ActiveTacticalMapTrackedComponent>();
            while (tracked.MoveNext(out var ent, out var comp))
            {
                if (comp == null)
                    continue;

                _toUpdate.Add((ent, comp));
            }
        }

        try
        {
            foreach (var update in _toUpdate)
            {
                if (!update.Comp.Running)
                    continue;

                UpdateTracked(update);
            }
        }
        finally
        {
            _toUpdate.Clear();
        }

        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var mapId, out var map))
        {
            if (!map.MapDirty || time < map.NextUpdate)
                continue;

            map.MapDirty = false;
            map.NextUpdate = time + _mapUpdateEvery;

            // dirty maps push one batched state update to every active viewer.
            var computers = EntityQueryEnumerator<ActiveTacticalMapComputerComponent, TacticalMapComputerComponent>();
            while (computers.MoveNext(out var computerId, out _, out var computer))
            {
                if (computer.Map != mapId)
                    continue;

                UpdateMapData((computerId, computer), map);
            }

            var users = EntityQueryEnumerator<ActiveTacticalMapUserComponent, TacticalMapUserComponent>();
            while (users.MoveNext(out var userId, out _, out var userComp))
            {
                if (userComp.Map != mapId)
                    continue;

                UpdateUserData((userId, userComp), map);
            }

            var tunnelUsers = EntityQueryEnumerator<TunnelUIUserComponent, TacticalMapUserComponent>();
            while (tunnelUsers.MoveNext(out var tunnelUserId, out _, out var tunnelUserComp))
            {
                if (tunnelUserComp.Map != mapId)
                    continue;

                UpdateUserData((tunnelUserId, tunnelUserComp), map);
            }

            var dropshipWeapons = EntityQueryEnumerator<TacticalMapComputerComponent, DropshipTerminalWeaponsComponent>();
            while (dropshipWeapons.MoveNext(out var weaponsId, out var weaponsComputer, out _))
            {
                if (!_ui.IsUiOpen(weaponsId, DropshipTerminalWeaponsUi.Key))
                    continue;

                if (weaponsComputer.Map != mapId)
                    continue;

                UpdateMapData((weaponsId, weaponsComputer), map);
            }

            _replay.RecordSnapshot(mapId, map);
        }
    }
}
