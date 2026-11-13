using System.Numerics;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Pulling;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared._Stories.ZLevels.Pulling;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Movement.Pulling.Systems;

/// <summary>
/// Allows one entity to pull another behind them via a physics distance joint.
/// </summary>
public sealed class PullingSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly HeldSpeedModifierSystem _clothingMoveSpeed = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _modifierSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtual = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;

    private bool _isRecreatingJoint;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
        UpdatesOutsidePrediction = true;

        SubscribeLocalEvent<PullableComponent, MoveInputEvent>(OnPullableMoveInput);
        SubscribeLocalEvent<PullableComponent, CollisionChangeEvent>(OnPullableCollisionChange);
        SubscribeLocalEvent<PullableComponent, JointRemovedEvent>(OnJointRemoved);
        SubscribeLocalEvent<PullableComponent, GetVerbsEvent<Verb>>(AddPullVerbs);
        SubscribeLocalEvent<PullableComponent, EntGotInsertedIntoContainerMessage>(OnPullableContainerInsert);
        SubscribeLocalEvent<PullableComponent, ModifyUncuffDurationEvent>(OnModifyUncuffDuration);
        SubscribeLocalEvent<PullableComponent, StopBeingPulledAlertEvent>(OnStopBeingPulledAlert);

        SubscribeLocalEvent<PullerComponent, UpdateMobStateEvent>(OnStateChanged, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<PullerComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<PullerComponent, EntGotInsertedIntoContainerMessage>(OnPullerContainerInsert);
        SubscribeLocalEvent<PullerComponent, EntityUnpausedEvent>(OnPullerUnpaused);
        SubscribeLocalEvent<PullerComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
        SubscribeLocalEvent<PullerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<PullerComponent, DropHandItemsEvent>(OnDropHandItems);
        SubscribeLocalEvent<PullerComponent, StopPullingAlertEvent>(OnStopPullingAlert);

        SubscribeLocalEvent<HandsComponent, PullStartedMessage>(HandlePullStarted);
        SubscribeLocalEvent<HandsComponent, PullStoppedMessage>(HandlePullStopped);

        SubscribeLocalEvent<PullableComponent, StrappedEvent>(OnBuckled);
        SubscribeLocalEvent<PullableComponent, BuckledEvent>(OnGotBuckled);
        // Stories-ZLevels-Start
        SubscribeLocalEvent<PullableComponent, EntParentChangedMessage>(OnPullableParentChanged);
        SubscribeLocalEvent<PullerComponent, EntParentChangedMessage>(OnPullerParentChanged);
        // Stories-ZLevels-End

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ReleasePulledObject, InputCmdHandler.FromDelegate(OnReleasePulledObject, handle: false))
            .Register<PullingSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActivePullerComponent, PullerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var pullerComp, out var xform))
        {
            if (pullerComp.Pulling is not { } pulledUid || !pulledUid.IsValid() || !TryComp<PullableComponent>(pulledUid, out var pullableComp))
                continue;

            if (HasComp<ZLevelTransitioningComponent>(uid) || HasComp<ZLevelTransitioningComponent>(pulledUid) ||
                HasComp<ZLevelPullingTransitionComponent>(uid) || HasComp<ZLevelPullingTransitionComponent>(pulledUid))
                continue;

            if (HasComp<BeingFiremanCarriedComponent>(pulledUid))
                continue;

            if (!TryComp<TransformComponent>(pulledUid, out var pulledXform))
                continue;

            if (xform.MapID != pulledXform.MapID || xform.MapID == MapId.Nullspace)
            {
                // Stories-ZLevels-Start
                if (xform.MapID != MapId.Nullspace && pulledXform.MapID != MapId.Nullspace &&
                    _zLevels.AreInSameNetwork(xform.MapID, pulledXform.MapID))
                {
                    // Ensure cross-map physics joint is removed so Box2D doesn't complain
                    if (pullableComp.PullJointId != null)
                    {
                        RemovePullJointDuringTransition(pulledUid, pullableComp);
                    }

                    var userPos = _transform.ToMapCoordinates(xform.Coordinates).Position;
                    var pulledPos = _transform.ToMapCoordinates(pulledXform.Coordinates).Position;
                    var zDist = Vector2.Distance(userPos, pulledPos);
                    if (zDist > 3.5f)
                    {
                        TryStopPull(pulledUid, pullableComp);
                        continue;
                    }

                    if (_net.IsServer &&
                        TryComp<PhysicsComponent>(pulledUid, out var pulledPhysics) &&
                        pulledPhysics.BodyType != BodyType.Static)
                    {
                        var dir = userPos - pulledPos;
                        var distSq = dir.LengthSquared();
                        if (distSq > 0.04f)
                        {
                            var pullDir = Vector2.Normalize(dir);
                            var speed = Math.Clamp(zDist * 2.5f, 3.5f, 6.0f);
                            _physics.SetLinearVelocity(pulledUid, pullDir * speed, body: pulledPhysics);
                        }
                    }
                    continue;
                }
                // Stories-ZLevels-End

                TryStopPull(pulledUid, pullableComp);
                continue;
            }

            if (!xform.Coordinates.TryDistance(EntityManager, pulledXform.Coordinates, out var dist) || dist > 3.5f)
            {
                TryStopPull(pulledUid, pullableComp);
                continue;
            }

            if (!_timing.ApplyingState)
            {
                var hasJoint = pullableComp.PullJointId != null &&
                               TryComp<JointComponent>(pulledUid, out var jointComp) &&
                               jointComp.GetJoints.ContainsKey(pullableComp.PullJointId);

                if (!hasJoint)
                {
                    if (HasComp<ZLevelTransitioningComponent>(uid) || HasComp<ZLevelTransitioningComponent>(pulledUid) ||
                        HasComp<ZLevelPullingTransitionComponent>(uid) || HasComp<ZLevelPullingTransitionComponent>(pulledUid))
                    {
                        continue;
                    }

                    if (dist > 3.5f || !CanPull(uid, pulledUid, pullerComp))
                    {
                        TryStopPull(pulledUid, pullableComp);
                    }
                    else
                    {
                        TryStartPull(uid, pulledUid, pullerComp, pullableComp);
                    }
                }
            }
        }
    }

    private void HandlePullStarted(EntityUid uid, HandsComponent component, PullStartedMessage args)
    {
        if (args.PullerUid != uid)
            return;

        if (TryComp(args.PullerUid, out PullerComponent? pullerComp) && !pullerComp.NeedsHands)
            return;

        if (!_virtual.TrySpawnVirtualItemInHand(args.PulledUid, uid))
        {
            DebugTools.Assert("Unable to find available hand when starting pulling??");
        }
    }

    private void HandlePullStopped(EntityUid uid, HandsComponent component, PullStoppedMessage args)
    {
        if (args.PullerUid != uid)
            return;

        // Stories-ZLevels-Start
        if (_isRecreatingJoint)
            return;

        if (HasComp<ZLevelTransitioningComponent>(args.PullerUid) || HasComp<ZLevelTransitioningComponent>(args.PulledUid) ||
            HasComp<ZLevelPullingTransitionComponent>(args.PullerUid) || HasComp<ZLevelPullingTransitionComponent>(args.PulledUid))
            return;

        if (args.PullerUid.IsValid() && args.PulledUid.IsValid() &&
            !TerminatingOrDeleted(args.PullerUid) && !TerminatingOrDeleted(args.PulledUid) &&
            TryComp<TransformComponent>(args.PullerUid, out var mapXformA) &&
            TryComp<TransformComponent>(args.PulledUid, out var mapXformB))
        {
            if (mapXformA.MapID != mapXformB.MapID && mapXformA.MapID != MapId.Nullspace && mapXformB.MapID != MapId.Nullspace && _zLevels.AreInSameNetwork(mapXformA.MapID, mapXformB.MapID))
                return;
        }
        // Stories-ZLevels-End

        // Try find hand that is doing this pull.
        // and clear it.
        foreach (var held in _handsSystem.EnumerateHeld((uid, component)))
        {
            if (!TryComp(held, out VirtualItemComponent? virtualItem) || virtualItem.BlockingEntity != args.PulledUid)
                continue;

            _handsSystem.TryDrop((args.PullerUid, component), held);
            break;
        }
    }

    private void OnStateChanged(EntityUid uid, PullerComponent component, ref UpdateMobStateEvent args)
    {
        if (component.Pulling == null || !component.Pulling.Value.IsValid())
            return;

        if (TryComp<PullableComponent>(component.Pulling, out var comp) && (args.State == MobState.Critical || args.State == MobState.Dead))
        {
            TryStopPull(component.Pulling.Value, comp);
        }
    }

    private void OnBuckled(Entity<PullableComponent> ent, ref StrappedEvent args)
    {
        // Prevent people from pulling the entity they are buckled to
        if (ent.Comp.Puller == args.Buckle.Owner && !args.Buckle.Comp.PullStrap)
            StopPulling(ent, ent);
    }

    private void OnGotBuckled(Entity<PullableComponent> ent, ref BuckledEvent args)
    {
        if (ent.Comp.Puller is { } puller &&
            _rmcPulling.TryRetargetPull(puller, ent) is { } retarget)
        {
            TryStartPull(puller, retarget);
            return;
        }

        StopPulling(ent, ent);
    }

    private void OnAfterState(Entity<PullerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Pulling == null || !ent.Comp.Pulling.Value.IsValid())
        {
            RemComp<ActivePullerComponent>(ent.Owner);
        }
        else
        {
            EnsureComp<ActivePullerComponent>(ent.Owner);
        }
    }

    // Stories-ZLevels-Start
    private void OnPullableParentChanged(Entity<PullableComponent> ent, ref EntParentChangedMessage args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (ent.Comp.Puller is not { } puller || !puller.IsValid() || TerminatingOrDeleted(puller))
            return;

        if (!TryComp<TransformComponent>(ent.Owner, out var myXform) ||
            !TryComp<TransformComponent>(puller, out var pullerXform))
            return;

        var myMap = myXform.MapID;
        var pullerMap = pullerXform.MapID;
        if (myMap != pullerMap || myMap == MapId.Nullspace)
        {
            RemovePullJointDuringTransition(ent.Owner, ent.Comp);
        }
    }

    private void OnPullerParentChanged(Entity<PullerComponent> ent, ref EntParentChangedMessage args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (ent.Comp.Pulling is not { } pulled || !pulled.IsValid() || TerminatingOrDeleted(pulled))
            return;

        if (!TryComp<TransformComponent>(ent.Owner, out var myXform) ||
            !TryComp<TransformComponent>(pulled, out var pulledXform))
            return;

        var myMap = myXform.MapID;
        var pulledMap = pulledXform.MapID;
        if (myMap != pulledMap || myMap == MapId.Nullspace)
        {
            if (TryComp<PullableComponent>(pulled, out var pullableComp))
            {
                RemovePullJointDuringTransition(pulled, pullableComp);
            }
        }
    }
    // Stories-ZLevels-End

    private void OnDropHandItems(EntityUid uid, PullerComponent pullerComp, DropHandItemsEvent args)
    {
        if (pullerComp.Pulling == null || !pullerComp.Pulling.Value.IsValid() || pullerComp.NeedsHands)
            return;

        if (!TryComp(pullerComp.Pulling, out PullableComponent? pullableComp))
            return;

        TryStopPull(pullerComp.Pulling.Value, pullableComp, uid);
    }

    private void OnStopPullingAlert(Entity<PullerComponent> ent, ref StopPullingAlertEvent args)
    {
        if (args.Handled)
            return;
        if (ent.Comp.Pulling is not { } pulling || !pulling.IsValid())
            return;
        if (!TryComp<PullableComponent>(pulling, out var pullable))
            return;
        args.Handled = TryStopPull(pulling, pullable, ent);
    }

    private void OnPullerContainerInsert(Entity<PullerComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (ent.Comp.Pulling == null || !ent.Comp.Pulling.Value.IsValid())
            return;

        if (!TryComp(ent.Comp.Pulling.Value, out PullableComponent? pulling))
            return;

        TryStopPull(ent.Comp.Pulling.Value, pulling, ent.Owner);
    }

    private void OnPullableContainerInsert(Entity<PullableComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        TryStopPull(ent.Owner, ent.Comp);
    }

    private void OnModifyUncuffDuration(Entity<PullableComponent> ent, ref ModifyUncuffDurationEvent args)
    {
        if (!ent.Comp.BeingPulled)
            return;

        // We don't care if the person is being uncuffed by someone else
        if (args.User != args.Target)
            return;

        args.Duration *= 2;
    }

    private void OnStopBeingPulledAlert(Entity<PullableComponent> ent, ref StopBeingPulledAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStopPull(ent, ent, ent);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<PullingSystem>();
    }

    private void OnPullerUnpaused(EntityUid uid, PullerComponent component, ref EntityUnpausedEvent args)
    {
        component.NextThrow += args.PausedTime;
    }

    private void OnVirtualItemDeleted(EntityUid uid, PullerComponent component, VirtualItemDeletedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        // If client deletes the virtual hand then stop the pull.
        if (component.Pulling == null || !component.Pulling.Value.IsValid())
            return;

        if (component.Pulling != args.BlockingEntity)
            return;

        if (TryComp(args.BlockingEntity, out PullableComponent? comp))
        {
            TryStopPull(args.BlockingEntity, comp);
        }
    }

    private void AddPullVerbs(EntityUid uid, PullableComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Are they trying to pull themselves up by their bootstraps?
        if (args.User == args.Target)
            return;

        //TODO VERB ICONS add pulling icon
        if (component.Puller == args.User)
        {
            Verb verb = new()
            {
                Text = Loc.GetString("pulling-verb-get-data-text-stop-pulling"),
                Act = () => TryStopPull(uid, component, user: args.User),
                DoContactInteraction = false // pulling handle its own contact interaction.
            };
            args.Verbs.Add(verb);
        }
        else if (CanPull(args.User, args.Target))
        {
            Verb verb = new()
            {
                Text = Loc.GetString("pulling-verb-get-data-text"),
                Act = () => TryStartPull(args.User, args.Target),
                DoContactInteraction = false // pulling handle its own contact interaction.
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnRefreshMovespeed(EntityUid uid, PullerComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (TryComp<HeldSpeedModifierComponent>(component.Pulling, out var heldMoveSpeed) && component.Pulling.HasValue)
        {
            var (walkMod, sprintMod) =
                _clothingMoveSpeed.GetHeldMovementSpeedModifiers(component.Pulling.Value, heldMoveSpeed);
            args.ModifySpeed(walkMod, sprintMod);
            return;
        }

        args.ModifySpeed(component.WalkSpeedModifier, component.SprintSpeedModifier);
    }

    private void OnPullableMoveInput(EntityUid uid, PullableComponent component, ref MoveInputEvent args)
    {
        // If someone moves then break their pulling.
        if (!component.BeingPulled)
            return;

        var entity = args.Entity;

        if (!_blocker.CanMove(entity))
            return;

        TryStopPull(uid, component, user: uid);
    }

    private void OnPullableCollisionChange(EntityUid uid, PullableComponent component, ref CollisionChangeEvent args)
    {
        // IDK what this is supposed to be.
        if (!_timing.ApplyingState && component.PullJointId != null && !args.CanCollide)
        {
            _joints.RemoveJoint(uid, component.PullJointId);
        }
    }

    private void OnJointRemoved(EntityUid uid, PullableComponent component, JointRemovedEvent args)
    {
        // Just handles the joint getting nuked without going through pulling system (valid behavior).

        // Not relevant / pullable state handle it.
        if (component.Puller != args.OtherEntity ||
            args.Joint.ID != component.PullJointId ||
            _timing.ApplyingState)
        {
            return;
        }

        if (args.Joint.ID != component.PullJointId || component.Puller == null)
            return;

        // Stories-ZLevels-Start
        if (_isRecreatingJoint)
            return;

        var pullerUid = component.Puller.Value;
        if (!pullerUid.IsValid() || TerminatingOrDeleted(pullerUid) || TerminatingOrDeleted(uid))
        {
            StopPulling(uid, component);
            return;
        }

        if (HasComp<ZLevelTransitioningComponent>(uid) || HasComp<ZLevelTransitioningComponent>(pullerUid) ||
            HasComp<ZLevelPullingTransitionComponent>(uid) || HasComp<ZLevelPullingTransitionComponent>(pullerUid))
            return;

        if (HasComp<BeingFiremanCarriedComponent>(uid) ||
            (TryComp<CanFiremanCarryComponent>(pullerUid, out var carrier) && carrier.Carrying == uid))
        {
            // Fireman carry entities are parented to the carrier and do not rely on Box2D physics joints
            return;
        }

        if (TryComp<TransformComponent>(uid, out var xformA) &&
            TryComp<TransformComponent>(pullerUid, out var xformB))
        {
            if (xformA.MapID != xformB.MapID && xformA.MapID != MapId.Nullspace && xformB.MapID != MapId.Nullspace && _zLevels.AreInSameNetwork(xformA.MapID, xformB.MapID))
            {
                // Cross-Z pulling continues virtually without Box2D joint!
                return;
            }
        }
        // Stories-ZLevels-End

        StopPulling(uid, component);
    }

    /// <summary>
    /// Forces pulling to stop and handles cleanup.
    /// </summary>
    private void StopPulling(EntityUid pullableUid, PullableComponent pullableComp)
    {
        if (pullableComp.Puller == null)
            return;

        // Stories-ZLevels-Start
        if (_isRecreatingJoint)
            return;

        if (HasComp<ZLevelTransitioningComponent>(pullableUid) ||
            (pullableComp.Puller is { } p && HasComp<ZLevelTransitioningComponent>(p)))
        {
            return;
        }
        // Stories-ZLevels-End

        if (!_timing.ApplyingState)
        {
            // Joint shutdown
            var jointId = pullableComp.PullJointId ?? $"pull-joint-{GetNetEntity(pullableUid)}";
            pullableComp.PullJointId = null;
            RemovePullJoint(pullableUid, pullableComp.Puller, jointId);

            if (TryComp<PhysicsComponent>(pullableUid, out var pullablePhysics))
            {
                _physics.SetFixedRotation(pullableUid, pullableComp.PrevFixedRotation, body: pullablePhysics);
            }
        }

        var oldPuller = pullableComp.Puller;
        if (oldPuller != null)
            RemComp<ActivePullerComponent>(oldPuller.Value);

        pullableComp.PullJointId = null;
        pullableComp.Puller = null;
        Dirty(pullableUid, pullableComp);

        // No more joints with puller -> force stop pull.
        if (TryComp<PullerComponent>(oldPuller, out var pullerComp))
        {
            var pullerUid = oldPuller.Value;
            _alertsSystem.ClearAlert(pullerUid, pullerComp.PullingAlert);
            pullerComp.Pulling = null;
            Dirty(oldPuller.Value, pullerComp);

            // Messaging
            var message = new PullStoppedMessage(pullerUid, pullableUid);
            _modifierSystem.RefreshMovementSpeedModifiers(pullerUid);
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(pullerUid):user} stopped pulling {ToPrettyString(pullableUid):target}");

            RaiseLocalEvent(pullerUid, message);
            RaiseLocalEvent(pullableUid, message);
        }

        _alertsSystem.ClearAlert(pullableUid, pullableComp.PulledAlert);
    }

    public bool IsPulled(EntityUid uid, PullableComponent? component = null)
    {
        return Resolve(uid, ref component, false) && component.BeingPulled;
    }

    public bool IsPulling(EntityUid puller, PullerComponent? component = null)
    {
        return Resolve(puller, ref component, false) && component.Pulling != null && component.Pulling.Value.IsValid();
    }

    public EntityUid? GetPuller(EntityUid puller, PullableComponent? component = null)
    {
        return !Resolve(puller, ref component, false) ? null : component.Puller;
    }

    public EntityUid? GetPulling(EntityUid puller, PullerComponent? component = null)
    {
        if (!Resolve(puller, ref component, false) || component.Pulling == null || !component.Pulling.Value.IsValid())
            return null;
        return component.Pulling;
    }

    private void OnReleasePulledObject(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { Valid: true } player)
        {
            return;
        }

        if (!TryComp(player, out PullerComponent? pullerComp) ||
            pullerComp.Pulling == null ||
            !pullerComp.Pulling.Value.IsValid() ||
            !TryComp(pullerComp.Pulling, out PullableComponent? pullableComp))
        {
            return;
        }

        TryStopPull(pullerComp.Pulling.Value, pullableComp, user: player);
    }

    public bool CanPull(EntityUid puller, EntityUid pullableUid, PullerComponent? pullerComp = null)
    {
        if (!Resolve(puller, ref pullerComp, false))
        {
            return false;
        }

        if (!TryComp<TransformComponent>(puller, out var pullerXform) ||
            !TryComp<TransformComponent>(pullableUid, out var pullableXform) ||
            pullerXform.MapID == MapId.Nullspace ||
            pullerXform.MapID != pullableXform.MapID)
        {
            return false;
        }

        if (pullerComp.NeedsHands
            && !_handsSystem.TryGetEmptyHand(puller, out _)
            && pullerComp.Pulling == null)
        {
            return false;
        }

        if (!_blocker.CanInteract(puller, pullableUid))
        {
            return false;
        }

        if (!TryComp<PhysicsComponent>(pullableUid, out var physics))
        {
            return false;
        }

        if (physics.BodyType == BodyType.Static)
        {
            return false;
        }

        if (puller == pullableUid)
        {
            return false;
        }

        if (!_containerSystem.IsInSameOrNoContainer(puller, pullableUid))
        {
            return false;
        }

        var getPulled = new BeingPulledAttemptEvent(puller, pullableUid);
        RaiseLocalEvent(pullableUid, getPulled, true);
        var startPull = new StartPullAttemptEvent(puller, pullableUid);
        RaiseLocalEvent(puller, startPull, true);
        return !startPull.Cancelled && !getPulled.Cancelled;
    }

    public bool TogglePull(Entity<PullableComponent?> pullable, EntityUid pullerUid)
    {
        if (!Resolve(pullable, ref pullable.Comp, false))
            return false;

        if (pullable.Comp.Puller == pullerUid)
        {
            var ev = new RMCPullToggleEvent();
            RaiseLocalEvent(pullerUid, ref ev);
            if (ev.Handled)
                return true;

            return TryStopPull(pullable, pullable.Comp);
        }

        return TryStartPull(pullerUid, pullable, pullableComp: pullable);
    }

    public bool TogglePull(EntityUid pullerUid, PullerComponent puller)
    {
        if (puller.Pulling is not { } pulling || !pulling.IsValid())
            return false;

        if (!TryComp<PullableComponent>(pulling, out var pullable))
            return false;

        return TogglePull((pulling, pullable), pullerUid);
    }

    public bool TryStartPull(EntityUid pullerUid, EntityUid pullableUid,
        PullerComponent? pullerComp = null, PullableComponent? pullableComp = null)
    {
        var ev = new RMCGetPullTargetEvent(pullerUid, pullableUid);
        RaiseLocalEvent(pullableUid, ref ev);

        if (pullableUid != ev.Target)
        {
            pullableUid = ev.Target;
            pullableComp = CompOrNull<PullableComponent>(pullableUid);
        }

        if (!Resolve(pullerUid, ref pullerComp, false) ||
            !Resolve(pullableUid, ref pullableComp, false))
        {
            return false;
        }

        if (pullerComp.Pulling == pullableUid)
        {
            // Stories-ZLevels-Start
            if (!_timing.ApplyingState &&
                TryComp(pullerUid, out PhysicsComponent? pPhysics) &&
                TryComp(pullableUid, out PhysicsComponent? bPhysics))
            {
                if (!TryComp<TransformComponent>(pullerUid, out var pullerXform) ||
                    !TryComp<TransformComponent>(pullableUid, out var pullableXform) ||
                    pullerXform.MapID == MapId.Nullspace ||
                    pullerXform.MapID != pullableXform.MapID)
                {
                    return false;
                }

                if (HasComp<ZLevelTransitioningComponent>(pullerUid) || HasComp<ZLevelTransitioningComponent>(pullableUid) ||
                    HasComp<ZLevelPullingTransitionComponent>(pullerUid) || HasComp<ZLevelPullingTransitionComponent>(pullableUid))
                {
                    return false;
                }

                if (pPhysics.BodyType == BodyType.Static || bPhysics.BodyType == BodyType.Static)
                    return false;

                var jointId = pullableComp.PullJointId ?? $"pull-joint-{GetNetEntity(pullableUid)}";
                pullableComp.PullJointId = jointId;

                RemovePullJoint(pullableUid, pullerUid, jointId);

                var joint = _joints.CreateDistanceJoint(pullableUid, pullerUid,
                        bPhysics.LocalCenter, pPhysics.LocalCenter,
                        id: pullableComp.PullJointId, minimumDistance: 1);
                if (joint != null)
                {
                    joint.CollideConnected = false;
                    joint.MaxLength = Math.Clamp(joint.Length + 0.15f, 0.5f, 1.25f);
                    joint.MinLength = 0f;
                    joint.Stiffness = 0f;
                }
                _physics.SetFixedRotation(pullableUid, pullableComp.FixedRotationOnPull, body: bPhysics);

                _physics.WakeBody(pullerUid, body: pPhysics);
                _physics.WakeBody(pullableUid, body: bPhysics);

                EnsureComp<ActivePullerComponent>(pullerUid);
                pullerComp.Pulling = pullableUid;
                pullableComp.Puller = pullerUid;

                _alertsSystem.ShowAlert(pullerUid, pullerComp.PullingAlert);
                _alertsSystem.ShowAlert(pullableUid, pullableComp.PulledAlert);

                if (_net.IsServer && pullerComp.NeedsHands && TryComp<HandsComponent>(pullerUid, out var handsComp))
                {
                    var hasVirtual = false;
                    foreach (var held in _handsSystem.EnumerateHeld((pullerUid, handsComp)))
                    {
                        if (TryComp<VirtualItemComponent>(held, out var vItem) && vItem.BlockingEntity == pullableUid)
                        {
                            hasVirtual = true;
                            break;
                        }
                    }

                    if (!hasVirtual)
                    {
                        _virtual.TrySpawnVirtualItemInHand(pullableUid, pullerUid);
                    }
                }

                Dirty(pullerUid, pullerComp);
                Dirty(pullableUid, pullableComp);
            }
            return true;
            // Stories-ZLevels-End
        }

        if (!CanPull(pullerUid, pullableUid))
            return false;

        if (!TryComp(pullerUid, out PhysicsComponent? pullerPhysics) || !TryComp(pullableUid, out PhysicsComponent? pullablePhysics))
            return false;

        // Ensure that the puller is not currently pulling anything.
        if (TryComp<PullableComponent>(pullerComp.Pulling, out var oldPullable)
            && !TryStopPull(pullerComp.Pulling.Value, oldPullable, pullerUid))
            return false;

        // Stop anyone else pulling the entity we want to pull
        if (pullableComp.Puller != null)
        {
            // We're already pulling this item
            if (pullableComp.Puller == pullerUid)
                return false;

            if (!TryStopPull(pullableUid, pullableComp, pullableComp.Puller))
                return false;
        }

        var pullAttempt = new PullAttemptEvent(pullerUid, pullableUid);
        RaiseLocalEvent(pullerUid, pullAttempt);

        if (pullAttempt.Cancelled)
            return false;

        RaiseLocalEvent(pullableUid, pullAttempt);

        if (pullAttempt.Cancelled)
            return false;

        // Pulling confirmed

        _interaction.DoContactInteraction(pullableUid, pullerUid);

        // Use net entity so it's consistent across client and server.
        pullableComp.PullJointId = $"pull-joint-{GetNetEntity(pullableUid)}";

        EnsureComp<ActivePullerComponent>(pullerUid);
        pullerComp.Pulling = pullableUid;
        pullableComp.Puller = pullerUid;

        // store the pulled entity's physics FixedRotation setting in case we change it
        pullableComp.PrevFixedRotation = pullablePhysics.FixedRotation;

        // joint state handling will manage its own state
        if (!_timing.ApplyingState)
        {
            if (TryComp<TransformComponent>(pullerUid, out var pullerXform) &&
                TryComp<TransformComponent>(pullableUid, out var pullableXform) &&
                pullerXform.MapID != MapId.Nullspace && pullerXform.MapID == pullableXform.MapID &&
                !HasComp<ZLevelTransitioningComponent>(pullerUid) && !HasComp<ZLevelTransitioningComponent>(pullableUid) &&
                !HasComp<ZLevelPullingTransitionComponent>(pullerUid) && !HasComp<ZLevelPullingTransitionComponent>(pullableUid) &&
                pullerPhysics.BodyType != BodyType.Static && pullablePhysics.BodyType != BodyType.Static)
            {
                var jointId = pullableComp.PullJointId ?? $"pull-joint-{GetNetEntity(pullableUid)}";
                pullableComp.PullJointId = jointId;

                RemovePullJoint(pullableUid, pullerUid, jointId);

                var joint = _joints.CreateDistanceJoint(pullableUid, pullerUid,
                        pullablePhysics.LocalCenter, pullerPhysics.LocalCenter,
                        id: pullableComp.PullJointId, minimumDistance: 1);
                if (joint != null)
                {
                    joint.CollideConnected = false;
                    joint.MaxLength = Math.Clamp(joint.Length + 0.15f, 0.5f, 1.25f);
                    joint.MinLength = 0f;
                    joint.Stiffness = 0f;
                }

                _physics.SetFixedRotation(pullableUid, pullableComp.FixedRotationOnPull, body: pullablePhysics);
            }
        }

        // Messaging
        var message = new PullStartedMessage(pullerUid, pullableUid);
        _modifierSystem.RefreshMovementSpeedModifiers(pullerUid);
        _alertsSystem.ShowAlert(pullerUid, pullerComp.PullingAlert);
        _alertsSystem.ShowAlert(pullableUid, pullableComp.PulledAlert);

        RaiseLocalEvent(pullerUid, message);
        RaiseLocalEvent(pullableUid, message);

        Dirty(pullerUid, pullerComp);
        Dirty(pullableUid, pullableComp);

        var pullingMessage =
            Loc.GetString("getting-pulled-popup", ("puller", Identity.Entity(pullerUid, EntityManager)));
        _popup.PopupEntity(pullingMessage, pullableUid, pullableUid);

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(pullerUid):user} started pulling {ToPrettyString(pullableUid):target}");
        return true;
    }

    public bool TryStopPull(EntityUid pullableUid, PullableComponent pullable, EntityUid? user = null)
    {
        var pullerUidNull = pullable.Puller;

        if (pullerUidNull == null)
            return true;

        if (user != null && !_blocker.CanInteract(user.Value, pullableUid))
            return false;

        var msg = new AttemptStopPullingEvent(user);
        RaiseLocalEvent(pullableUid, msg, true);

        if (msg.Cancelled)
            return false;

        StopPulling(pullableUid, pullable);
        return true;
    }

    // Stories-ZLevels-Start
    public void RemovePullJointDuringTransition(EntityUid pullableUid, PullableComponent? pullableComp = null)
    {
        if (!Resolve(pullableUid, ref pullableComp, false))
            return;

        var puller = pullableComp.Puller;
        var jointId = pullableComp.PullJointId ?? $"pull-joint-{GetNetEntity(pullableUid)}";
        pullableComp.PullJointId = null;

        RemovePullJoint(pullableUid, puller, jointId);
    }

    private void RemovePullJoint(EntityUid pullableUid, EntityUid? pullerUid, string? jointId)
    {
        _isRecreatingJoint = true;
        try
        {
            Joint? targetJoint = null;

            if (TryComp<JointComponent>(pullableUid, out var jA))
            {
                if (jointId != null && jA.GetJoints.TryGetValue(jointId, out var directJointA))
                {
                    targetJoint = directJointA;
                }
                else if (pullerUid != null)
                {
                    foreach (var jointVal in jA.GetJoints.Values)
                    {
                        if (jointVal.BodyAUid == pullerUid || jointVal.BodyBUid == pullerUid)
                        {
                            targetJoint = jointVal;
                            break;
                        }
                    }
                }
            }

            if (targetJoint == null && pullerUid != null && TryComp<JointComponent>(pullerUid.Value, out var jB))
            {
                if (jointId != null && jB.GetJoints.TryGetValue(jointId, out var directJointB))
                {
                    targetJoint = directJointB;
                }
                else
                {
                    foreach (var jointVal in jB.GetJoints.Values)
                    {
                        if (jointVal.BodyAUid == pullableUid || jointVal.BodyBUid == pullableUid)
                        {
                            targetJoint = jointVal;
                            break;
                        }
                    }
                }
            }

            if (targetJoint != null)
            {
                EnsureComp<JointComponent>(targetJoint.BodyAUid);
                EnsureComp<JointComponent>(targetJoint.BodyBUid);
                _joints.RemoveJoint(targetJoint);
            }

            if (jointId != null)
            {
                if (TryComp<JointComponent>(pullableUid, out var jAComp) && jAComp.GetJoints.ContainsKey(jointId))
                    _joints.RemoveJoint(pullableUid, jointId);

                if (pullerUid != null && TryComp<JointComponent>(pullerUid.Value, out var jBComp) && jBComp.GetJoints.ContainsKey(jointId))
                    _joints.RemoveJoint(pullerUid.Value, jointId);
            }
        }
        finally
        {
            _isRecreatingJoint = false;
        }
    }
    // Stories-ZLevels-End
}
