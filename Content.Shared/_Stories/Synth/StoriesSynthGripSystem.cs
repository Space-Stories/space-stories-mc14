using Content.Shared._RMC14.Synth;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
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
}
