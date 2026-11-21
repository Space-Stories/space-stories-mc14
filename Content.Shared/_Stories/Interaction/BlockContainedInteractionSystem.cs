using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Containers;

namespace Content.Shared._Stories.Interaction;

public sealed class BlockContainedInteractionSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlockContainedInteractionComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<BlockContainedInteractionComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<BlockContainedInteractionComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BlockContainedInteractionComponent, GettingUsedAttemptEvent>(OnGettingUsedAttempt);
    }

    public bool IsBlocked(EntityUid uid, EntityUid user)
    {
        if (_hands.IsHolding(user, uid))
            return false;

        return true;
    }

    public bool HasUtensil(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<UtensilComponent>(held))
                return true;
        }

        return false;
    }

    private void OnActivateInWorld(Entity<BlockContainedInteractionComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<FoodComponent>(ent) && HasUtensil(args.User))
            return;

        if (IsBlocked(ent, args.User))
            args.Handled = true;
    }

    private void OnUseInHand(Entity<BlockContainedInteractionComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (IsBlocked(ent, args.User))
            args.Handled = true;
    }

    private void OnInteractUsing(Entity<BlockContainedInteractionComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<UtensilComponent>(args.Used))
            return;

        if (IsBlocked(ent, args.User))
            args.Handled = true;
    }

    private void OnGettingUsedAttempt(Entity<BlockContainedInteractionComponent> ent, ref GettingUsedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (IsBlocked(ent, args.User))
            args.Cancel();
    }
}
