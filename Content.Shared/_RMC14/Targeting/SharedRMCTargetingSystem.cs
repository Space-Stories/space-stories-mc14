using Content.Shared._RMC14.GameStates;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Rangefinder;
using Content.Shared._RMC14.Rangefinder.Spotting;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Targeting;

public abstract class SharedRMCTargetingSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedRMCPvsSystem _rmcPvs = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TargetingComponent, ComponentRemove>(OnTargetingRemoved);
        SubscribeLocalEvent<TargetingComponent, EntityTerminatingEvent>(OnTargetingRemoved); // Stories-FixDeletedTargetingEntities
        SubscribeLocalEvent<TargetingComponent, DroppedEvent>(OnTargetingDropped);
        SubscribeLocalEvent<TargetingComponent, RMCDroppedEvent>(OnTargetingDropped);
        SubscribeLocalEvent<TargetingComponent, GotUnequippedHandEvent>(OnTargetingDropped);
        SubscribeLocalEvent<TargetingComponent, HandDeselectedEvent>(OnTargetingDropped);

        SubscribeLocalEvent<RMCTargetedComponent, ComponentRemove>(OnTargetedRemove);
        SubscribeLocalEvent<RMCTargetedComponent, EntityTerminatingEvent>(OnTargetedRemove);
    }

    /// <summary>
    ///     Remove any lasers if the <see cref="TargetingComponent"/> is being removed from an entity.
    /// </summary>
    private void OnTargetingRemoved<T>(Entity<TargetingComponent> targeting, ref T args)
    {
        if (_net.IsClient)
            return;

        if(TryComp(targeting, out RangefinderComponent? rangefinder))
            _appearance.SetData(targeting, RangefinderLayers.Layer, rangefinder.Mode);

        while (targeting.Comp.Targets.Count > 0)
        {
            StopTargeting((targeting, targeting), targeting.Comp.Targets[0]);
        }

        foreach (var session in _player.Sessions)
        {
            _rmcPvs.RemoveSessionOverride(targeting.Owner, session);
        }
    }

    /// <summary>
    ///     Stop all targeting done by the entity if it's interrupted.
    /// </summary>
    private void OnTargetingDropped<T>(Entity<TargetingComponent> targeting, ref T args)
    {
        if (_net.IsClient)
            return;

        var ev = new TargetingCancelledEvent();
        RaiseLocalEvent(targeting, ref ev);

        while (targeting.Comp.Targets.Count > 0)
        {
            StopTargeting((targeting, targeting), targeting.Comp.Targets[0]);
        }
    }

    private void OnTargetedRemove<T>(Entity<RMCTargetedComponent> ent, ref T args)
    {
        if (_net.IsClient)
            return;

        // Stories-FixDeletedTargetingEntities-Start
        foreach (var targeting in ent.Comp.TargetedBy.ToArray())
        {
            if (!TryComp(targeting, out TargetingComponent? targetingComp))
                continue;

            while (targetingComp.Targets.Remove(ent)) {}
            targetingComp.LaserDurations.Remove(ent);
            targetingComp.OriginalLaserDurations.Remove(ent);
            Dirty(targeting, targetingComp);

            if (targetingComp.Targets.Count == 0)
                RemComp<TargetingComponent>(targeting);
        }
        // Stories-FixDeletedTargetingEntities-End

        foreach (var session in _player.Sessions)
        {
            _rmcPvs.RemoveSessionOverride(ent, session);
        }
    }

    /// <summary>
    ///     Remove the laser and replace targeting effects originating from the given entity.
    /// </summary>
    /// <param name="targeting">The entity that is targeting</param>
    /// <param name="target">The entity being targeted</param>
    public void StopTargeting(Entity<TargetingComponent?> targeting, EntityUid target)
    {
        if (!Resolve(targeting, ref targeting.Comp, false))
            return;

        // Stories-FixDeletedTargetingEntities-Start
        while (targeting.Comp.Targets.Remove(target)) {}
        targeting.Comp.LaserDurations.Remove(target);
        targeting.Comp.OriginalLaserDurations.Remove(target);
        Dirty(targeting);

        if (TryComp(target, out RMCTargetedComponent? targeted))
        {
            targeted.TargetedBy.Remove(targeting);
            targeted.AlphaMultipliers.Remove(targeting);
            Dirty(target, targeted);

            var highestMark = TargetedEffects.None;
            var spotters = false;

            foreach (var activeLaser in targeted.TargetedBy)
            {
                if (!TryComp(activeLaser, out TargetingComponent? comp))
                    continue;

                if (comp.LaserType > highestMark)
                    highestMark = comp.LaserType;

                if (comp.LaserType == TargetedEffects.Spotted)
                    spotters = true;
            }

            targeted.TargetType = highestMark;
            Dirty(target, targeted);

            if (!spotters)
                RemComp<SpottedComponent>(target);

            if (targeted.TargetedBy.Count == 0)
                RemComp<RMCTargetedComponent>(target);
        }

        if (targeting.Comp.Targets.Count == 0)
            RemComp<TargetingComponent>(targeting);
        // Stories-FixDeletedTargetingEntities-End
    }

    /// <summary>
    ///     Apply the <see cref="TargetingComponent"/> to the entity creating the laser.
    ///     Apply the <see cref="RMCTargetedComponent"/> to the entity being targeted.
    /// </summary>
    /// <param name="equipment">The entity that is targeting</param>
    /// <param name="user">The user of the targeting entity</param>
    /// <param name="target">The entity being targeted</param>
    /// <param name="targetingDuration">How long the targeting should last if not interrupted</param>
    /// <param name="targetedEffect">The visualiser to apply on the entity being targeted</param>
    /// <param name="showDirection">If a direction indicator pointing towards the targeting entity should be displayed</param>
    public void Target(EntityUid equipment, EntityUid user, EntityUid target, float targetingDuration, TargetedEffects targetedEffect = TargetedEffects.None, bool showDirection = false)
    {
        // Stories-FixDeletedTargetingEntities-Start
        if (TerminatingOrDeleted(equipment) || TerminatingOrDeleted(user) || TerminatingOrDeleted(target) ||
            !Exists(equipment) || !Exists(user) || !Exists(target))
        {
            return;
        }
        // Stories-FixDeletedTargetingEntities-End

        // Change the laser and targeting effect if focused.
        var ev = new TargetingStartedEvent(targetedEffect, target);
        RaiseLocalEvent(equipment, ref ev);
        targetedEffect = ev.TargetedEffect;

        var targeted = EnsureComp<RMCTargetedComponent>(target);
        // Stories-FixDeletedTargetingEntities-Start
        if (!targeted.TargetedBy.Contains(equipment))
            targeted.TargetedBy.Add(equipment);
        // Stories-FixDeletedTargetingEntities-End
        targeted.ShowDirection = showDirection;
        if (targetedEffect > targeted.TargetType)
            targeted.TargetType = targetedEffect;
        Dirty(target, targeted);

        var targeting = EnsureComp<TargetingComponent>(equipment);
        if (!targeting.LaserDurations.TryAdd(target, new List<float>{targetingDuration}))
            targeting.LaserDurations[target].Add(targetingDuration);

        if (!targeting.OriginalLaserDurations.TryAdd(target, new List<float>{targetingDuration}))
            targeting.OriginalLaserDurations[target].Add(targetingDuration);

        targeting.Source = equipment;
        // Stories-FixDeletedTargetingEntities-Start
        if (!targeting.Targets.Contains(target))
            targeting.Targets.Add(target);
        // Stories-FixDeletedTargetingEntities-End
        targeting.Origin = Transform(user).Coordinates;
        targeting.User = user;
        targeting.LaserType = targetedEffect;
        Dirty(equipment, targeting);

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity == null)
                continue;

            _rmcPvs.AddSessionOverride(target, session);
            _rmcPvs.AddSessionOverride(equipment, session);
        }
    }

    /// <summary>
    ///     Remove a laser pointing to a specific target,
    /// </summary>
    /// <param name="ent">The targeting entity</param>
    /// <param name="target">The target the laser is pointing to</param>
    /// <param name="laserNumber">The position of the laser in the list of lasers connecting to the target</param>
    // Stories-FixDeletedTargetingEntities-Start
    private void RemoveLaser(Entity<TargetingComponent> ent, EntityUid target, int laserNumber)
    {
        if (ent.Comp.LaserDurations.TryGetValue(target, out var durations) && laserNumber < durations.Count)
            durations.RemoveAt(laserNumber);

        if (ent.Comp.OriginalLaserDurations.TryGetValue(target, out var origDurations) && laserNumber < origDurations.Count)
            origDurations.RemoveAt(laserNumber);

        if (ent.Comp.LaserDurations.TryGetValue(target, out var remaining) && remaining.Count <= 0)
        {
            ent.Comp.LaserDurations.Remove(target);
            ent.Comp.OriginalLaserDurations.Remove(target);
        }

        Dirty(ent);
    }
    // Stories-FixDeletedTargetingEntities-End

    /// <summary>
    ///     Reduce the duration of targeting effects every frame.
    /// </summary>
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TargetingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var targeting, out var xform))
        {
            // Stories-FixDeletedTargetingEntities-Start
            if (TerminatingOrDeleted(targeting.User) ||
                TerminatingOrDeleted(targeting.Source) ||
                !Exists(targeting.User) ||
                !Exists(targeting.Source) ||
                TerminatingOrDeleted(targeting.Origin.EntityId) ||
                !Exists(targeting.Origin.EntityId))
            {
                RemCompDeferred<TargetingComponent>(uid);
                continue;
            }
            // Stories-FixDeletedTargetingEntities-End

            var targetNumber = 0;
            var checkedTargets = new List<EntityUid>();
            while (targetNumber < targeting.Targets.Count)
            {
                var target = targeting.Targets[targetNumber];
                // Stories-FixDeletedTargetingEntities-Start
                if (TerminatingOrDeleted(target) || !Exists(target))
                {
                    StopTargeting((uid, targeting), target);
                    continue;
                }
                // Stories-FixDeletedTargetingEntities-End

                if (!targeting.LaserDurations.ContainsKey(target) || checkedTargets.Contains(target))
                {
                    // Stories-FixDeletedTargetingEntities-Start
                    if (!targeting.LaserDurations.ContainsKey(target))
                    {
                        StopTargeting((uid, targeting), target);
                        continue;
                    }
                    // Stories-FixDeletedTargetingEntities-End
                    targetNumber++;
                    continue;
                }

                checkedTargets.Add(target);

                var laserNumber = 0;
                while (laserNumber < targeting.LaserDurations[target].Count)
                {
                    targeting.LaserDurations[target][laserNumber] -= frameTime;
                    Dirty(uid, targeting);

                    // Adjust alpha of the laser based on how close it is to finishing targeting.
                    if (TryComp(target, out RMCTargetedComponent? targeted))
                    {
                        var newAlpha = 1 - targeting.LaserDurations[target][laserNumber] / targeting.OriginalLaserDurations[target][laserNumber];
                        targeted.AlphaMultipliers.TryAdd(uid, newAlpha);
                        if (newAlpha > targeted.AlphaMultipliers[uid])
                            targeted.AlphaMultipliers[uid] =  newAlpha;
                    }

                    // Raise an event and stop targeting if the targeting successfully finishes.
                    if (targeting.LaserDurations[target][laserNumber] <= 0)
                    {
                        RemoveLaser((uid, targeting), target, laserNumber);
                        if (TryComp(target, out TransformComponent? targetTransform))
                        {
                            var ev = new TargetingFinishedEvent(targeting.User, targetTransform.Coordinates, target);
                            RaiseLocalEvent(uid, ref ev);
                        }

                        // Stories-FixDeletedTargetingEntities-Start
                        if (!targeting.LaserDurations.ContainsKey(target) || targeting.LaserDurations[target].Count == 0)
                        {
                            StopTargeting((uid, targeting), target);
                        }
                        // Stories-FixDeletedTargetingEntities-End
                        break;
                    }
                    laserNumber++;
                }
                targetNumber++;
            }

            // Stories-FixDeletedTargetingEntities-Start
            if (!TryComp(uid, out targeting))
                continue;
            // Stories-FixDeletedTargetingEntities-End

            // Remove the active component and raise an event if the user moves.
            if (TryComp(xform.ParentUid, out TransformComponent? parentTransform) &&
                !_transform.InRange(parentTransform.Coordinates, targeting.Origin, 0.1f))
            {
                var ev = new TargetingCancelledEvent();
                RaiseLocalEvent(uid, ref ev);

                targeting.LaserDurations.Clear();
                targeting.OriginalLaserDurations.Clear();
                Dirty(uid, targeting);
                while (targeting.Targets.Count > 0)
                {
                    StopTargeting((uid, targeting), targeting.Targets[0]);
                }
            }
        }
    }
}

/// <summary>
///     Raised on an entity when it finishes targeting.
/// </summary>
[ByRefEvent]
public record struct TargetingFinishedEvent(EntityUid User, EntityCoordinates Coordinates, EntityUid Target, bool Handled = false);

/// <summary>
///     Raised on an entity when it's targeting is interrupted.
/// </summary>
[ByRefEvent]
public record struct TargetingCancelledEvent(bool Handled = false);

/// <summary>
///     Raised on an entity when it starts targeting.
/// </summary>
[ByRefEvent]
public record struct TargetingStartedEvent(TargetedEffects TargetedEffect, EntityUid Target);
