using Content.Shared.Access.Components;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.Access.Systems;
public sealed class ActivatableUIRequiresAccessSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivatableUIRequiresAccessComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        // Stories-InterfaceSecurity-Start
        SubscribeLocalEvent<ActivatableUIRequiresAccessComponent, BoundUserInterfaceMessageAttempt>(OnBoundUIAttempt);
        // Stories-InterfaceSecurity-End
    }

    private void OnUIOpenAttempt(Entity<ActivatableUIRequiresAccessComponent> activatableUI, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_access.IsAllowed(args.User, activatableUI))
        {
            args.Cancel();
            if (activatableUI.Comp.PopupMessage != null)
                _popup.PopupClient(Loc.GetString(activatableUI.Comp.PopupMessage), activatableUI, args.User);
        }
    }

    // Stories-InterfaceSecurity-Start
    private void OnBoundUIAttempt(Entity<ActivatableUIRequiresAccessComponent> activatableUI, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled)
            return;

        if (HasComp<GhostComponent>(args.Actor))
            return;

        if (!_access.IsAllowed(args.Actor, activatableUI))
        {
            args.Cancel();
            if (activatableUI.Comp.PopupMessage != null)
                _popup.PopupClient(Loc.GetString(activatableUI.Comp.PopupMessage), activatableUI, args.Actor);
        }
    }
    // Stories-InterfaceSecurity-End
}

