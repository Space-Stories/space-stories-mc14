using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared._Stories.ZLevels.Flight.Components;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Mobs;
using Content.Shared.Stunnable;
using JetBrains.Annotations;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.ZLevels.Flight;

public abstract partial class SharedZFlightSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedZLevelsSystem _zLevel = default!;

    protected EntityQuery<ZPhysicsComponent> ZPhyzQuery;

    public override void Initialize()
    {
        base.Initialize();
        InitializeControllable();

        ZPhyzQuery = GetEntityQuery<ZPhysicsComponent>();

        SubscribeLocalEvent<ZPhysicsComponent, FlightStartedEvent>(OnStartFlight);
        SubscribeLocalEvent<ZPhysicsComponent, FlightStoppedEvent>(OnStopFlight);
        SubscribeLocalEvent<ZFlyerComponent, ZGetVelocityEvent>(OnGetZVelocity);
        SubscribeLocalEvent<ZFlyerComponent, ZCheckGravityEvent>(OnGetGravity);
        SubscribeLocalEvent<ZFlyerComponent, IsWeightlessEvent>(OnCheckWeightless);

        SubscribeLocalEvent<ZFlyerComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<ZFlyerComponent, KnockedDownEvent>(OnKnockDowned);
        SubscribeLocalEvent<ZFlyerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ZFlyerComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnCheckWeightless(Entity<ZFlyerComponent> ent, ref IsWeightlessEvent args)
    {
        if (!ent.Comp.Active || args.Handled)
            return;

        args.IsWeightless = true;
        args.Handled = true;
    }

    private void OnDamageChanged(Entity<ZFlyerComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (!args.InterruptsDoAfters)
            return;

        DeactivateFlight((ent, ent));
    }

    private void OnMobStateChanged(Entity<ZFlyerComponent> ent, ref MobStateChangedEvent args)
    {
        DeactivateFlight((ent, ent));
    }

    private void OnKnockDowned(Entity<ZFlyerComponent> ent, ref KnockedDownEvent args)
    {
        DeactivateFlight((ent, ent));
    }

    private void OnStunned(Entity<ZFlyerComponent> ent, ref StunnedEvent args)
    {
        DeactivateFlight((ent, ent));
    }

    private void OnStartFlight(Entity<ZPhysicsComponent> ent, ref FlightStartedEvent args)
    {
        SetTargetHeight(ent.Owner, ent.Comp.CurrentZLevel);
        StartFlightVisuals(ent.Owner);
    }

    private void OnStopFlight(Entity<ZPhysicsComponent> ent, ref FlightStoppedEvent args)
    {
        StopFlightVisuals(ent.Owner);
    }

    private void OnGetZVelocity(Entity<ZFlyerComponent> ent, ref ZGetVelocityEvent args)
    {
        if (!ent.Comp.Active)
            return;

        var zPhys = args.Target.Comp;
        var currentPos = zPhys.CurrentZLevel + zPhys.LocalPosition;
        var targetPos = ent.Comp.TargetMapHeight + 0.2f;
        var currentVelocity = zPhys.Velocity;

        var distanceToTarget = targetPos - currentPos;

        var targetVelocity = Math.Clamp(distanceToTarget * ent.Comp.FlightSpeed, -ent.Comp.FlightSpeed, ent.Comp.FlightSpeed);
        var velocityDelta = targetVelocity - currentVelocity;

        var upperBound = ent.Comp.TargetMapHeight + 0.9f;
        var lowerBound = ent.Comp.TargetMapHeight + 0.1f;

        var newVelocity = currentVelocity + velocityDelta;
        var nextPos = currentPos + newVelocity;

        if (nextPos > upperBound)
        {
            var maxAllowedVelocity = upperBound - currentPos;
            velocityDelta = maxAllowedVelocity - currentVelocity;
        }
        else if (nextPos < lowerBound)
        {
            var maxAllowedVelocity = lowerBound - currentPos;
            velocityDelta = maxAllowedVelocity - currentVelocity;
        }

        args.VelocityDelta = velocityDelta;
    }

    private void OnGetGravity(Entity<ZFlyerComponent> ent, ref ZCheckGravityEvent args)
    {
        if (ent.Comp.Active)
            args.Gravity *= 0;
    }

    [PublicAPI]
    public bool TryActivateFlight(Entity<ZFlyerComponent?> ent, ZPhysicsComponent? zPhys = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!Resolve(ent, ref zPhys, false))
            return false;

        if (ent.Comp.Active)
            return false;

        var ev = new StartFlightAttemptEvent();
        RaiseLocalEvent(ent, ev);

        if (ev.Cancelled)
            return false;

        ent.Comp.Active = true;
        Dirty(ent, ent.Comp);

        zPhys.VelocityRaiseEvent = true;

        _zLevel.UpdateGravityState((ent, zPhys));
        _zLevel.WakeBody((ent, zPhys));
        _gravity.RefreshWeightless(ent.Owner);

        RaiseLocalEvent(ent, new FlightStartedEvent());
        return true;
    }

    [PublicAPI]
    public void DeactivateFlight(Entity<ZFlyerComponent?> ent, ZPhysicsComponent? zPhys = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!Resolve(ent, ref zPhys, false))
            return;

        if (!ent.Comp.Active)
            return;

        ent.Comp.Active = false;
        Dirty(ent, ent.Comp);

        zPhys.VelocityRaiseEvent = false;

        _zLevel.UpdateGravityState((ent, zPhys));
        _gravity.RefreshWeightless(ent.Owner);

        RaiseLocalEvent(ent, new FlightStoppedEvent());
    }

    [PublicAPI]
    public void SetTargetHeight(Entity<ZFlyerComponent?> ent, int targetHeight)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.TargetMapHeight = targetHeight;
        Dirty(ent, ent.Comp);
    }

    private void StartFlightVisuals(Entity<ZFlyerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        _appearance.SetData(ent, ZFlightVisuals.Active, true);
        _ambient.SetAmbience(ent, true);
    }

    private void StopFlightVisuals(Entity<ZFlyerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        _appearance.SetData(ent, ZFlightVisuals.Active, false);
        _ambient.SetAmbience(ent, false);
    }
}

/// <summary>
/// Called on an entity when it attempts to start flight mode. Subscribe and cancel this event if you want to cancel your flight for any reason.
/// </summary>
public sealed partial class StartFlightAttemptEvent : CancellableEntityEventArgs;

/// <summary>
/// Called on an entity when it enters flight mode
/// </summary>
public sealed partial class FlightStartedEvent : EntityEventArgs;

/// <summary>
/// Called on an entity when it exits flight mode
/// </summary>
public sealed partial class FlightStoppedEvent : EntityEventArgs;

/// <summary>
/// Instant Action, raising the target flight level by 1
/// </summary>
public sealed partial class ZFlightActionUp : InstantActionEvent
{
}

/// <summary>
/// Instant Action, lowering the target flight level by 1
/// </summary>
public sealed partial class ZFlightActionDown : InstantActionEvent
{
}

[Serializable, NetSerializable]
public enum ZFlightVisuals
{
    Active,
}

/// <summary>
/// DoAfter event for starting flight with a delay
/// </summary>
[Serializable, NetSerializable]
public sealed partial class StartFlightDoAfterEvent : SimpleDoAfterEvent
{
}

