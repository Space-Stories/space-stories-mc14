using Content.Shared._Stories.Strip;
using Content.Shared.ActionBlocker;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Stories.Synth.WorkingJoe;

public sealed class StoriesUnstrippableWhileAbleSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<STUnstrippableWhileAbleComponent, IsUnequippingTargetAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<STUnstrippableWhileAbleComponent, IsEquippingTargetAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<STUnstrippableWhileAbleComponent, STStripHandAttemptEvent>(OnStripHandAttempt);
    }

    private void OnStripHandAttempt(Entity<STUnstrippableWhileAbleComponent> ent, ref STStripHandAttemptEvent args)
    {
        if (args.Cancelled || !IsBlocking(ent, args.User))
            return;

        args.Reason = args.Insert ? ent.Comp.EquipReason : ent.Comp.UnequipReason;
        args.Cancelled = true;
    }

    private void OnUnequipAttempt(Entity<STUnstrippableWhileAbleComponent> ent, ref IsUnequippingTargetAttemptEvent args)
    {
        if (args.Cancelled || !IsBlocking(ent, args.Unequipee))
            return;

        args.Reason = ent.Comp.UnequipReason;
        args.Cancel();
    }

    private void OnEquipAttempt(Entity<STUnstrippableWhileAbleComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (args.Cancelled || !IsBlocking(ent, args.Equipee))
            return;

        args.Reason = ent.Comp.EquipReason;
        args.Cancel();
    }

    private bool IsBlocking(EntityUid target, EntityUid actor)
    {
        return actor != target && _actionBlocker.CanInteract(target, null);
    }
}
