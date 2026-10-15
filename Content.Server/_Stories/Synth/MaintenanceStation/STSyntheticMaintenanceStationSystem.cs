using System.Numerics;
using Content.Server.Atmos.Rotting;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Synth;
using Content.Shared._Stories.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Synth;

public sealed class STSyntheticMaintenanceStationSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly RottingSystem _rotting = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private static readonly Vector2[] EjectOffsets =
    {
        new(0, -1), new(1, 0), new(0, 1), new(-1, 0),
        new(1, -1), new(1, 1), new(-1, 1), new(-1, -1),
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, STSyntheticMaintenanceStationInsertDoAfterEvent>(OnInsertDoAfter);
    }

    private void OnComponentInit(Entity<STSyntheticMaintenanceStationComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _container.EnsureContainer<ContainerSlot>(ent, STSyntheticMaintenanceStationComponent.BodyContainerId);
        ent.Comp.CurrentInternalCharge = Math.Min(ent.Comp.CurrentInternalCharge, ent.Comp.MaxInternalCharge);
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
        UpdateAppearance(ent);
    }

    private void OnRelayMovement(Entity<STSyntheticMaintenanceStationComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        Eject(ent);
    }

    private void OnGetInteractionVerbs(Entity<STSyntheticMaintenanceStationComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (args.Using == null ||
            !args.CanAccess ||
            !args.CanInteract ||
            !CanInsert(ent, args.Using.Value))
        {
            return;
        }

        var target = args.Using.Value;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => StartInsertDoAfter(ent, user, target),
            Category = VerbCategory.Insert,
            Text = Name(target),
        });
    }

    private void OnGetAlternativeVerbs(Entity<STSyntheticMaintenanceStationComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (IsOccupied(ent.Comp))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => Eject(ent),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("st-synthetic-maintenance-station-eject-verb"),
                Priority = 1,
            });
            return;
        }

        if (!CanInsert(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => Insert(ent, user, user),
            Category = VerbCategory.Insert,
            Text = Loc.GetString("st-synthetic-maintenance-station-enter-verb"),
        });
    }

    private void OnDragDrop(Entity<STSyntheticMaintenanceStationComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !CanInsert(ent, args.Dragged))
            return;

        if (args.User == args.Dragged)
            args.Handled = Insert(ent, args.Dragged, args.User);
        else
            args.Handled = StartInsertDoAfter(ent, args.User, args.Dragged);
    }

    private void OnDestroyed(Entity<STSyntheticMaintenanceStationComponent> ent, ref DestructionEventArgs args)
    {
        Eject(ent);
    }

    private void OnExamined(Entity<STSyntheticMaintenanceStationComponent> ent, ref ExaminedEvent args)
    {
        var percent = ent.Comp.MaxInternalCharge <= 0
            ? 0
            : Math.Round(ent.Comp.CurrentInternalCharge / ent.Comp.MaxInternalCharge * 100);

        using (args.PushGroup(nameof(STSyntheticMaintenanceStationComponent)))
        {
            args.PushMarkup(Loc.GetString("st-synthetic-maintenance-station-charge-examine", ("charge", percent)));
        }
    }

    private void OnInsertDoAfter(Entity<STSyntheticMaintenanceStationComponent> ent, ref STSyntheticMaintenanceStationInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var inserted = GetEntity(args.Inserted);
        Insert(ent, inserted, args.User);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<STSyntheticMaintenanceStationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.UpdateInterval;
            Process((uid, comp));
        }
    }

    private void Process(Entity<STSyntheticMaintenanceStationComponent> ent)
    {
        var powered = this.IsPowered(ent.Owner, EntityManager);
        var oldCharge = ent.Comp.CurrentInternalCharge;

        var occupied = ent.Comp.BodyContainer.ContainedEntity != null;
        var rechargeRate = occupied ? ent.Comp.ActiveRechargeRate : ent.Comp.PassiveRechargeRate;
        if (powered)
            ent.Comp.CurrentInternalCharge = Math.Min(ent.Comp.MaxInternalCharge, ent.Comp.CurrentInternalCharge + rechargeRate);
        else
            ent.Comp.CurrentInternalCharge = Math.Max(0, ent.Comp.CurrentInternalCharge - ent.Comp.UnpoweredDrainRate);

        if (ent.Comp.BodyContainer.ContainedEntity is not { } contained)
        {
            if (Math.Abs(oldCharge - ent.Comp.CurrentInternalCharge) > 0.01f)
                UpdateAppearance(ent);

            return;
        }

        if (!powered || ent.Comp.CurrentInternalCharge <= 0)
        {
            _popup.PopupEntity(Loc.GetString("st-synthetic-maintenance-station-power-fail"), ent, contained);
            Eject(ent);
            return;
        }

        TryRevive(contained, ent);

        if (TryRepair(contained, ent))
            ent.Comp.CurrentInternalCharge = Math.Max(0, ent.Comp.CurrentInternalCharge - ent.Comp.RepairChargeCost);
        else if (TryRestoreBlood(contained, ent))
            ent.Comp.CurrentInternalCharge = Math.Max(0, ent.Comp.CurrentInternalCharge - ent.Comp.RepairChargeCost);
        else
        {
            _popup.PopupEntity(Loc.GetString("st-synthetic-maintenance-station-complete"), ent, contained);
            Eject(ent);
        }

        UpdateAppearance(ent);
    }

    private bool TryRepair(EntityUid contained, Entity<STSyntheticMaintenanceStationComponent> station)
    {
        if (!TryComp(contained, out DamageableComponent? damageable) ||
            damageable.TotalDamage <= FixedPoint2.Zero)
        {
            return false;
        }

        var delta = _damageable.TryChangeDamage(contained,
            station.Comp.RepairDamage,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable,
            origin: station.Owner);

        if (delta is not { Empty: false })
            return false;

        TryRevive(contained, station);
        return true;
    }

    private void TryRevive(EntityUid contained, Entity<STSyntheticMaintenanceStationComponent> station)
    {
        if (!TryComp(contained, out DamageableComponent? damageable) ||
            !TryComp<MobStateComponent>(contained, out var mobState) ||
            !_mobState.IsDead(contained, mobState) ||
            !_mobThreshold.TryGetThresholdForState(contained, MobState.Dead, out var deadThreshold) ||
            damageable.TotalDamage >= deadThreshold)
        {
            return;
        }

        if (_rotting.IsRotten(contained) || HasComp<UnrevivableComponent>(contained))
            return;

        _mobState.ChangeMobState(contained, MobState.Critical, mobState, station.Owner);
        _mobThreshold.VerifyThresholds(contained);

        if (_mind.TryGetMind(contained, out _, out var mind) &&
            mind.CurrentEntity != contained &&
            _player.TryGetSessionById(mind.UserId, out var session))
        {
            _euiManager.OpenEui(new ReturnToBodyEui(mind, _mind, _player), session);
        }
    }

    private bool TryRestoreBlood(EntityUid contained, Entity<STSyntheticMaintenanceStationComponent> station)
    {
        if (!TryComp(contained, out BloodstreamComponent? bloodstream) ||
            _bloodstream.GetBloodLevelPercentage((contained, bloodstream)) >= 0.999f)
        {
            return false;
        }

        return _bloodstream.TryModifyBloodLevel((contained, bloodstream), station.Comp.BloodRestoreAmount);
    }

    private bool StartInsertDoAfter(Entity<STSyntheticMaintenanceStationComponent> station, EntityUid user, EntityUid target)
    {
        if (!CanInsert(station, target) || !CheckPower(station, user))
            return false;

        var ev = new STSyntheticMaintenanceStationInsertDoAfterEvent(GetNetEntity(target));
        var doAfter = new DoAfterArgs(EntityManager, user, station.Comp.InsertDelay, ev, station, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private bool Insert(Entity<STSyntheticMaintenanceStationComponent> station, EntityUid target, EntityUid user)
    {
        if (!CanInsert(station, target) || !CheckPower(station, user))
            return false;

        if (!_container.Insert(target, station.Comp.BodyContainer))
            return false;

        _popup.PopupEntity(Loc.GetString("st-synthetic-maintenance-station-enter", ("target", target)), station.Owner);
        UpdateAppearance(station);
        return true;
    }

    private void Eject(Entity<STSyntheticMaintenanceStationComponent> station)
    {
        if (station.Comp.BodyContainer.ContainedEntity is not { } contained)
            return;

        _container.Remove(contained, station.Comp.BodyContainer);
        _transform.SetCoordinates(contained, GetEjectCoordinates(station.Owner, contained));
        if (station.Comp.ExitStun > TimeSpan.Zero)
            _stun.TryStun(contained, station.Comp.ExitStun, true);

        UpdateAppearance(station);
    }

    private EntityCoordinates GetEjectCoordinates(EntityUid station, EntityUid ejected)
    {
        var origin = Transform(station).Coordinates;
        var mask = TryComp(ejected, out PhysicsComponent? physics) && physics.CollisionMask != 0
            ? (CollisionGroup) physics.CollisionMask
            : CollisionGroup.MobMask;
        EntityCoordinates? fallback = null;

        foreach (var offset in EjectOffsets)
        {
            var coords = origin.Offset(offset);
            if (!_turf.TryGetTileRef(coords, out var tile) || _turf.IsSpace(tile.Value))
                continue;

            if (!_turf.IsTileBlocked(tile.Value, mask))
                return _turf.GetTileCenter(tile.Value);

            if (fallback == null && !_rmcMap.TileHasAnyTag(coords, WallTag))
                fallback = _turf.GetTileCenter(tile.Value);
        }

        return fallback ?? origin;
    }

    private bool CanInsert(Entity<STSyntheticMaintenanceStationComponent> station, EntityUid target)
    {
        if (IsOccupied(station.Comp) ||
            !HasComp<SynthComponent>(target) ||
            TerminatingOrDeleted(target))
        {
            return false;
        }

        return _container.CanInsert(target, station.Comp.BodyContainer);
    }

    private bool CheckPower(Entity<STSyntheticMaintenanceStationComponent> station, EntityUid user)
    {
        if (this.IsPowered(station.Owner, EntityManager) && station.Comp.CurrentInternalCharge > 0)
            return true;

        _popup.PopupEntity(Loc.GetString("stories-synthetic-maintenance-station-no-power", ("station", station.Owner)), station, user);
        return false;
    }

    private bool IsOccupied(STSyntheticMaintenanceStationComponent component)
    {
        return component.BodyContainer.ContainedEntity != null;
    }

    private void UpdateAppearance(Entity<STSyntheticMaintenanceStationComponent> ent)
    {
        if (!TryComp(ent, out AppearanceComponent? appearance))
            return;

        var occupied = IsOccupied(ent.Comp);
        if (ent.Comp.Occupied != occupied)
        {
            ent.Comp.Occupied = occupied;
            Dirty(ent);
        }

        var status = !this.IsPowered(ent.Owner, EntityManager) || ent.Comp.CurrentInternalCharge <= 0
            ? STSyntheticMaintenanceStationStatus.Off
            : occupied
                ? STSyntheticMaintenanceStationStatus.Occupied
                : STSyntheticMaintenanceStationStatus.Empty;

        _appearance.SetData(ent, STSyntheticMaintenanceStationVisuals.Status, status, appearance);
        _appearance.SetData(ent, STSyntheticMaintenanceStationVisuals.Charge, GetCharge(ent), appearance);
    }

    private STSyntheticMaintenanceStationCharge GetCharge(Entity<STSyntheticMaintenanceStationComponent> ent)
    {
        if (ent.Comp.MaxInternalCharge <= 0)
            return STSyntheticMaintenanceStationCharge.Empty;

        var fraction = ent.Comp.CurrentInternalCharge / ent.Comp.MaxInternalCharge;
        return fraction switch
        {
            <= 0.05f => STSyntheticMaintenanceStationCharge.Empty,
            <= 0.35f => STSyntheticMaintenanceStationCharge.Low,
            <= 0.65f => STSyntheticMaintenanceStationCharge.Medium,
            <= 0.85f => STSyntheticMaintenanceStationCharge.High,
            _ => STSyntheticMaintenanceStationCharge.Full,
        };
    }
}
