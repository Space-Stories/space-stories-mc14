using Content.Shared._RMC14.Synth;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Synth;

public sealed class StoriesSynthGripSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthComponent, PullStartedMessage>(OnSynthPullStarted);
        SubscribeLocalEvent<SynthComponent, PullStoppedMessage>(OnSynthPullStopped);

        SubscribeLocalEvent<STSynthGrippedComponent, ComponentStartup>(OnGrippedStartup);
        SubscribeLocalEvent<STSynthGrippedComponent, ComponentRemove>(OnGrippedRemove);
        SubscribeLocalEvent<STSynthGrippedComponent, UpdateCanMoveEvent>(OnGrippedCanMove);
        SubscribeLocalEvent<STSynthGrippedComponent, StopBeingPulledAlertEvent>(OnGrippedStopBeingPulledAlert, before: [typeof(PullingSystem)]);
        SubscribeLocalEvent<STSynthGrippedComponent, BuckleAttemptEvent>(OnGrippedBuckleAttempt);

        SubscribeLocalEvent<STSynthGrippedComponent, InteractionAttemptEvent>(OnGrippedInteractAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, ChangeDirectionAttemptEvent>(OnGrippedAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, UseAttemptEvent>(OnGrippedAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, ThrowAttemptEvent>(OnGrippedAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, DropAttemptEvent>(OnGrippedAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, AttackAttemptEvent>(OnGrippedAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, PickupAttemptEvent>(OnGrippedAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, IsEquippingAttemptEvent>(OnGrippedEquipAttempt);
        SubscribeLocalEvent<STSynthGrippedComponent, IsUnequippingAttemptEvent>(OnGrippedUnequipAttempt);
    }

    private void OnSynthPullStarted(Entity<SynthComponent> ent, ref PullStartedMessage args)
    {
        if (ent.Owner != args.PullerUid || _timing.ApplyingState)
            return;

        EnsureComp<STSynthGrippedComponent>(args.PulledUid);
    }

    private void OnSynthPullStopped(Entity<SynthComponent> ent, ref PullStoppedMessage args)
    {
        if (ent.Owner != args.PullerUid || _timing.ApplyingState)
            return;

        RemComp<STSynthGrippedComponent>(args.PulledUid);
    }

    private void OnGrippedStartup(Entity<STSynthGrippedComponent> ent, ref ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(ent);
    }

    private void OnGrippedRemove(Entity<STSynthGrippedComponent> ent, ref ComponentRemove args)
    {
        _actionBlocker.UpdateCanMove(ent);
    }

    private void OnGrippedCanMove(Entity<STSynthGrippedComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        if (!TryComp(ent, out PullableComponent? pullable) || pullable.Puller == null)
            return;

        args.Cancel();
    }

    private void OnGrippedStopBeingPulledAlert(Entity<STSynthGrippedComponent> ent, ref StopBeingPulledAlertEvent args)
    {
        args.Handled = true;
    }

    private void OnGrippedBuckleAttempt(Entity<STSynthGrippedComponent> ent, ref BuckleAttemptEvent args)
    {
        if (!TryComp(ent, out PullableComponent? pullable) || pullable.Puller == null)
            return;

        if (args.User == pullable.Puller)
            return;

        args.Cancelled = true;
    }

    private bool IsGripped(Entity<STSynthGrippedComponent> ent)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return false;

        return TryComp(ent, out PullableComponent? pullable) && pullable.Puller != null;
    }

    private void OnGrippedInteractAttempt(Entity<STSynthGrippedComponent> ent, ref InteractionAttemptEvent args)
    {
        if (IsGripped(ent))
            args.Cancelled = true;
    }

    private void OnGrippedAttempt<T>(Entity<STSynthGrippedComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        if (IsGripped(ent))
            args.Cancel();
    }

    private void OnGrippedEquipAttempt(Entity<STSynthGrippedComponent> ent, ref IsEquippingAttemptEvent args)
    {
        if (args.Equipee == ent.Owner && IsGripped(ent))
            args.Cancel();
    }

    private void OnGrippedUnequipAttempt(Entity<STSynthGrippedComponent> ent, ref IsUnequippingAttemptEvent args)
    {
        if (args.Unequipee == ent.Owner && IsGripped(ent))
            args.Cancel();
    }
}
