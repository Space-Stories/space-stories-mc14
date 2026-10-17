using Content.Shared.DoAfter;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Turns the instant stand-up on knockdown expiry into a GetUpDelay-long DoAfter.</summary>
public sealed class JuggernautGetUpSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<JuggernautArmorWornComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<JuggernautGettingUpComponent, StandAttemptEvent>(OnGettingUpStandAttempt);
        SubscribeLocalEvent<JuggernautGettingUpComponent, JuggernautGetUpDoAfterEvent>(OnGetUpDoAfter);
    }

    private void OnStandAttempt(Entity<JuggernautArmorWornComponent> ent, ref StandAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Auto-recovery only - stun system cancels while knockdown is still active.
        if (!TryComp(ent, out KnockedDownComponent? knocked) ||
            knocked.LifeStage <= ComponentLifeStage.Running)
        {
            return;
        }

        args.Cancel();

        // Server-only: the marker and DoAfter replicate down on their own.
        if (_net.IsClient)
            return;

        EnsureComp<JuggernautGettingUpComponent>(ent);

        var ev = new JuggernautGetUpDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, ent, ent.Comp.GetUpDelay, ev, ent)
        {
            // Uninterruptible - cancelling leaves him down forever with no retry.
            BreakOnMove = false,
            NeedHand = false,
            RequireCanInteract = false,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnGettingUpStandAttempt(Entity<JuggernautGettingUpComponent> ent, ref StandAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnGetUpDoAfter(Entity<JuggernautGettingUpComponent> ent, ref JuggernautGetUpDoAfterEvent args)
    {
        RemComp<JuggernautGettingUpComponent>(ent);

        if (args.Cancelled)
            return;

        args.Handled = true;

        if (HasComp<KnockedDownComponent>(ent))
            return;

        _standing.Stand(ent);
    }
}

[Serializable, NetSerializable]
public sealed partial class JuggernautGetUpDoAfterEvent : SimpleDoAfterEvent;
