using Content.Shared.Ghost;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using ActivatableUISystem = Content.Shared.UserInterface.ActivatableUISystem;

namespace Content.Server.Power.EntitySystems;

public sealed class ActivatableUIRequiresPowerSystem : SharedActivatableUIRequiresPowerSystem
{
    [Dependency] private readonly ActivatableUISystem _activatableUI = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivatableUIRequiresPowerComponent, PowerChangedEvent>(OnPowerChanged);
        // Stories-InterfaceSecurity-Start
        SubscribeLocalEvent<ActivatableUIRequiresPowerComponent, BoundUserInterfaceMessageAttempt>(OnBoundUIAttempt);
        // Stories-InterfaceSecurity-End
    }

    protected override void OnActivate(Entity<ActivatableUIRequiresPowerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || this.IsPowered(ent.Owner, EntityManager))
        {
            return;
        }

        args.Cancel();
    }

    // Stories-InterfaceSecurity-Start
    private void OnBoundUIAttempt(Entity<ActivatableUIRequiresPowerComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled || HasComp<GhostComponent>(args.Actor) || this.IsPowered(ent.Owner, EntityManager))
            return;

        args.Cancel();
    }
    // Stories-InterfaceSecurity-End

    private void OnPowerChanged(EntityUid uid, ActivatableUIRequiresPowerComponent component, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            _activatableUI.CloseAll(uid);
    }
}
