using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Xenonids.AcidBloodSplash;
using Content.Server._Stories.Ordnance.Explosion;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Deathcloud;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._Stories.Ordnance;
using Content.Shared._Stories.Ordnance.Simulator;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Ordnance.Simulator;

public sealed class DemolitionsSimulatorSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly OrdnanceExplosionSystem _ordnanceExplosion = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    private readonly HashSet<EntityUid> _arenaMaps = new();
    private readonly HashSet<EntityUid> _cleanupCache = new();
    private readonly List<ReagentQuantity> _reagentCache = new();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("demolitions.simulator");

        SubscribeLocalEvent<DemolitionsSimulatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, EntInsertedIntoContainerMessage>(OnItemChanged);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, EntRemovedFromContainerMessage>(OnItemChanged);

        SubscribeLocalEvent<DemolitionsSimulatorComponent, DemolitionsSimulatorDetonateMessage>(OnDetonate);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, DemolitionsSimulatorResetMessage>(OnResetArena);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, DemolitionsSimulatorEjectMessage>(OnEject);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, DemolitionsSimulatorSwitchCategoryMessage>(OnSwitchCategory);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, DemolitionsSimulatorSwitchProtoMessage>(OnSwitchProto);

        SubscribeLocalEvent<DemolitionsSimulatorComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, BoundUIClosedEvent>(OnUIClosed);
    }

    private void OnUIOpened(Entity<DemolitionsSimulatorComponent> ent, ref BoundUIOpenedEvent args)
    {
        EnsureArena(ent);
        if (ent.Comp.Camera != null && TryGetEntity(ent.Comp.Camera, out var cam))
        {
            if (TryComp<ActorComponent>(args.Actor, out var actor))
                _viewSubscriber.AddViewSubscriber(cam.Value, actor.PlayerSession);
        }
    }

    private void OnUIClosed(Entity<DemolitionsSimulatorComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.Camera != null && TryGetEntity(ent.Comp.Camera, out var cam))
        {
            if (TryComp<ActorComponent>(args.Actor, out var actor))
                _viewSubscriber.RemoveViewSubscriber(cam.Value, actor.PlayerSession);
        }
    }

    private void OnMapInit(Entity<DemolitionsSimulatorComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.SelectedPrototype.Id) &&
            ent.Comp.SpawnCategories.TryGetValue(ent.Comp.SelectedCategory, out var protos) &&
            protos.Count > 0)
        {
            ent.Comp.SelectedPrototype = protos[0];
        }
        EnsureArena(ent);
    }

    private void EnsureArena(Entity<DemolitionsSimulatorComponent> ent)
    {
        if (ent.Comp.ArenaMap != null) return;

        if (!_mapLoader.TryLoadGeneric(ent.Comp.ArenaMapPath, out var maps, out var grids, null))
            return;

        var mapEnt = maps.FirstOrDefault();
        if (mapEnt.Owner == default) return;

        var mapUid = mapEnt.Owner;
        var mapId = mapEnt.Comp.MapId;

        var gridEnt = grids.FirstOrDefault();
        var gridUid = gridEnt.Owner;

        _maps.InitializeMap(mapId);
        _maps.SetPaused(mapId, false);

        ent.Comp.ArenaMap = GetNetEntity(mapUid);
        _arenaMaps.Add(mapUid);

        if (gridUid != default)
            ent.Comp.ArenaGrid = GetNetEntity(gridUid);

        var targetUid = gridUid == default ? mapUid : gridUid;
        var centerCoords = new EntityCoordinates(targetUid, new Vector2(-0.5f, -0.5f));

        var camera = Spawn(ent.Comp.CameraPrototype, centerCoords);
        ent.Comp.Camera = GetNetEntity(camera);

        Dirty(ent);

        if (!string.IsNullOrEmpty(ent.Comp.SelectedPrototype.Id))
        {
            ResetAndSpawn(ent);
        }
    }

    private void OnRemove(Entity<DemolitionsSimulatorComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.ArenaMap != null)
        {
            var mapUid = GetEntity(ent.Comp.ArenaMap.Value);
            _arenaMaps.Remove(mapUid);
            if (!Deleted(mapUid))
                QueueDel(mapUid);
        }
    }

    private void OnInsertAttempt(Entity<DemolitionsSimulatorComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled) return;

        if (ent.Comp.RequiredSkills != null && args.User != null && !_skills.HasSkills(args.User.Value, ent.Comp.RequiredSkills))
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("stories-demo-sim-no-skill"), ent.Owner, args.User.Value);
            return;
        }

        if (!HasComp<OrdnanceCasingComponent>(args.Item))
        {
            args.Cancelled = true;
            if (args.User != null)
                _popup.PopupEntity(Loc.GetString("stories-demo-sim-invalid-item"), ent.Owner, args.User.Value);
        }
    }

    private void OnItemChanged(Entity<DemolitionsSimulatorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        Dirty(ent);
    }

    private void OnItemChanged(Entity<DemolitionsSimulatorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        Dirty(ent);
    }

    private bool CanInteractWithSimulator(Entity<DemolitionsSimulatorComponent> ent, EntityUid user)
    {
        if (!user.IsValid() || Deleted(user))
            return false;

        if (!_ui.IsUiOpen(ent.Owner, DemolitionsSimulatorUiKey.Key, user))
            return false;

        if (ent.Comp.RequiredSkills != null && !_skills.HasSkills(user, ent.Comp.RequiredSkills))
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-sim-no-skill"), ent.Owner, user);
            return false;
        }

        return true;
    }

    private void OnEject(Entity<DemolitionsSimulatorComponent> ent, ref DemolitionsSimulatorEjectMessage args)
    {
        if (!CanInteractWithSimulator(ent, args.Actor))
            return;

        if (ent.Comp.IsSimulating)
            return;

        if (_itemSlots.TryGetSlot(ent.Owner, ent.Comp.ItemSlotId, out var slot))
        {
            _itemSlots.TryEjectToHands(ent.Owner, slot, args.Actor);
        }
    }

    private void OnSwitchCategory(Entity<DemolitionsSimulatorComponent> ent, ref DemolitionsSimulatorSwitchCategoryMessage args)
    {
        if (!CanInteractWithSimulator(ent, args.Actor))
            return;

        if (ent.Comp.IsSimulating)
            return;

        if (string.IsNullOrEmpty(args.Category))
            return;

        if (!ent.Comp.SpawnCategories.TryGetValue(args.Category, out var protos) || protos.Count == 0)
        {
            _sawmill.Warning($"User {args.Actor} sent invalid category '{args.Category}' to DemolitionsSimulator {ent.Owner}.");
            return;
        }

        if (_timing.CurTime < ent.Comp.NextResetTime)
            return;

        ent.Comp.NextResetTime = _timing.CurTime + ent.Comp.ResetCooldown;
        ent.Comp.SelectedCategory = args.Category;
        ent.Comp.SelectedPrototype = protos[0];
        ResetAndSpawn(ent);
        Dirty(ent);
    }

    private void OnSwitchProto(Entity<DemolitionsSimulatorComponent> ent, ref DemolitionsSimulatorSwitchProtoMessage args)
    {
        if (!CanInteractWithSimulator(ent, args.Actor))
            return;

        if (ent.Comp.IsSimulating)
            return;

        if (string.IsNullOrEmpty(args.Prototype.Id))
            return;

        if (!ent.Comp.SpawnCategories.TryGetValue(ent.Comp.SelectedCategory, out var allowedProtos) ||
            !allowedProtos.Contains(args.Prototype))
        {
            _sawmill.Warning($"User {args.Actor} sent unauthorized prototype '{args.Prototype}' to DemolitionsSimulator {ent.Owner}.");
            return;
        }

        if (!_prototypeManager.HasIndex<EntityPrototype>(args.Prototype))
        {
            _sawmill.Warning($"User {args.Actor} sent non-existent prototype '{args.Prototype}' to DemolitionsSimulator {ent.Owner}.");
            return;
        }

        if (_timing.CurTime < ent.Comp.NextResetTime)
            return;

        ent.Comp.NextResetTime = _timing.CurTime + ent.Comp.ResetCooldown;
        ent.Comp.SelectedPrototype = args.Prototype;
        ResetAndSpawn(ent);
        Dirty(ent);
    }

    private void OnDetonate(Entity<DemolitionsSimulatorComponent> ent, ref DemolitionsSimulatorDetonateMessage args)
    {
        if (!CanInteractWithSimulator(ent, args.Actor))
            return;

        if (ent.Comp.IsSimulating)
            return;

        if (_timing.CurTime < ent.Comp.NextDetonationTime)
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-sim-cooldown"), ent.Owner, args.Actor);
            return;
        }

        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.ItemSlotId) is not { } item)
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-sim-empty"), ent.Owner, args.Actor);
            return;
        }

        if (!TryComp<OrdnanceCasingComponent>(item, out var casing))
            return;

        if (ent.Comp.ArenaMap == null || ent.Comp.Camera == null)
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-sim-no-arena"), ent.Owner, args.Actor);
            return;
        }

        var mapUid = GetEntity(ent.Comp.ArenaMap.Value);
        if (Deleted(mapUid))
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-sim-no-arena"), ent.Owner, args.Actor);
            return;
        }

        var gridUid = ent.Comp.ArenaGrid != null ? GetEntity(ent.Comp.ArenaGrid.Value) : mapUid;
        if (Deleted(gridUid))
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-sim-no-arena"), ent.Owner, args.Actor);
            return;
        }

        var effectiveCasing = _ordnanceExplosion.GetEffectiveCasing(item, casing);
        _reagentCache.Clear();
        _ordnanceExplosion.PopulateContentsCache(item, casing, _reagentCache);
        var stats = _ordnanceExplosion.CalculateExplosionStats(_reagentCache, casing, effectiveCasing);

        var arenaCoords = new EntityCoordinates(gridUid, new Vector2(-0.5f, -0.5f));

        ent.Comp.NextDetonationTime = _timing.CurTime + ent.Comp.Cooldown;
        ent.Comp.IsSimulating = true;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("stories-demo-sim-success"), ent.Owner, args.Actor);

        var actor = args.Actor;
        var entUid = ent.Owner;

        Timer.Spawn(ent.Comp.SimulationDelay, () =>
        {
            if (TerminatingOrDeleted(entUid))
                return;

            if (TryComp<DemolitionsSimulatorComponent>(entUid, out var simComp))
            {
                simComp.IsSimulating = false;
                Dirty(entUid, simComp);
            }

            if (Deleted(gridUid))
                return;

            _ordnanceExplosion.ExecuteExplosion(arenaCoords, stats, effectiveCasing, gridUid, actor);
        });
    }

    private void OnResetArena(Entity<DemolitionsSimulatorComponent> ent, ref DemolitionsSimulatorResetMessage args)
    {
        if (!CanInteractWithSimulator(ent, args.Actor))
            return;

        if (ent.Comp.IsSimulating)
            return;

        if (_timing.CurTime < ent.Comp.NextResetTime)
            return;

        ent.Comp.NextResetTime = _timing.CurTime + ent.Comp.ResetCooldown;
        ResetAndSpawn(ent);
        _popup.PopupEntity(Loc.GetString("stories-demo-sim-reset-done"), ent.Owner, args.Actor);
    }

    private void ResetAndSpawn(Entity<DemolitionsSimulatorComponent> ent)
    {
        if (ent.Comp.ArenaMap == null || string.IsNullOrEmpty(ent.Comp.SelectedPrototype.Id))
            return;

        if (!ent.Comp.SpawnCategories.TryGetValue(ent.Comp.SelectedCategory, out var allowedProtos) ||
            !allowedProtos.Contains(ent.Comp.SelectedPrototype))
        {
            return;
        }

        if (!_prototypeManager.HasIndex<EntityPrototype>(ent.Comp.SelectedPrototype))
            return;

        var mapUid = GetEntity(ent.Comp.ArenaMap.Value);
        if (Deleted(mapUid))
            return;

        var gridUid = ent.Comp.ArenaGrid != null ? GetEntity(ent.Comp.ArenaGrid.Value) : mapUid;
        if (Deleted(gridUid))
            return;

        var cameraUid = ent.Comp.Camera != null ? GetEntity(ent.Comp.Camera.Value) : EntityUid.Invalid;

        var markersQuery = EntityQueryEnumerator<DemolitionsArenaComponent, TransformComponent>();
        var spawnPoints = new List<EntityCoordinates>();

        while (markersQuery.MoveNext(out var markerUid, out _, out var xform))
        {
            if (xform.MapUid == mapUid)
                spawnPoints.Add(xform.Coordinates);
        }

        if (spawnPoints.Count == 0)
        {
            spawnPoints.Add(new EntityCoordinates(gridUid, new Vector2(-0.5f, -0.5f)));
        }

        _cleanupCache.Clear();
        foreach (var point in spawnPoints)
        {
            _lookup.GetEntitiesInRange(point, ent.Comp.CleanupRadius, _cleanupCache);
        }

        foreach (var uid in _cleanupCache)
        {
            if (uid != cameraUid && uid != gridUid && uid != mapUid && !HasComp<DemolitionsArenaComponent>(uid))
            {
                Del(uid);
            }
        }

        foreach (var point in spawnPoints)
        {
            try
            {
                var dummy = Spawn(ent.Comp.SelectedPrototype, point);
                EnsureComp<DemolitionsSimulatorDummyComponent>(dummy);
                RemCompDeferred<GhostRoleComponent>(dummy);
                RemCompDeferred<GhostTakeoverAvailableComponent>(dummy);
                _xeno.MakeDummyXeno(dummy);
                RemCompDeferred<XenoAnnounceDeathComponent>(dummy);
                RemCompDeferred<AcidBloodSplashComponent>(dummy);
                RemCompDeferred<XenoDeathcloudComponent>(dummy);
                RemCompDeferred<AutoAssignHiveComponent>(dummy);
                RemCompDeferred<HiveMemberComponent>(dummy);
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Exception spawning dummy '{ent.Comp.SelectedPrototype}' in DemolitionsSimulator: {ex}");
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DemolitionsSimulatorDummyComponent, HiveMemberComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            RemCompDeferred<HiveMemberComponent>(uid);
        }
    }
}
