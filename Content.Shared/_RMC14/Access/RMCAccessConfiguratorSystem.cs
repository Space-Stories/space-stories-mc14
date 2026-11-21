using Content.Shared._RMC14.UserInterface;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.UserInterface;
using static Content.Shared.Access.Components.AccessOverriderComponent;

namespace Content.Shared._RMC14.Access;

public sealed class RMCAccessConfiguratorSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly RMCUserInterfaceSystem _rmcUi = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCAccessConfiguratorComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
    }

    private void OnUiMessageAttempt(Entity<RMCAccessConfiguratorComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.UiKey is not AccessOverriderUiKey.Key || !_rmcUi.CanUseUI(args)) // Stories-EngineeringUiValidation (reuse the existing fork check)
        {
            args.Cancel();
            return;
        }

        if (!HasComp<BypassInteractionChecksComponent>(args.Actor) && !_hands.IsHolding(args.Actor, ent))
        {
            args.Cancel();
            return;
        }

        if (args.Message is not WriteToTargetAccessReaderIdMessage message)
            return;

        if (!TryComp<AccessOverriderComponent>(ent, out var overrider) ||
            overrider.PrivilegedIdSlot.Item is not { } card ||
            TerminatingOrDeleted(card) ||
            !_accessReader.IsAllowed(card, ent) ||
            TerminatingOrDeleted(overrider.TargetAccessReaderId) ||
            !_interaction.InRangeAndAccessible(args.Actor, overrider.TargetAccessReaderId) ||
            message.AccessList.Count > overrider.AccessLevels.Count)
        {
            args.Cancel();
        }
    }
}
