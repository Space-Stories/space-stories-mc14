using Content.Shared._RMC14.Synth;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;

namespace Content.Shared._Stories.Synth;

public sealed class STSynthItemRestrictionSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<STSynthItemRestrictionComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<STSynthItemRestrictionComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<STSynthItemRestrictionComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<STSynthItemRestrictionComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<STSynthItemRestrictionComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<STSynthItemRestrictionComponent, BeforeRangedInteractEvent>(OnBeforeRangedInteract);
    }

    private void OnPickupAttempt(Entity<STSynthItemRestrictionComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.CheckPickup || IsAllowed(ent.Comp, args.User))
            return;

        args.Cancel();
        Popup(ent, args.User);
    }

    private void OnEquipAttempt(Entity<STSynthItemRestrictionComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.CheckEquip || IsAllowed(ent.Comp, args.EquipTarget))
            return;

        args.Cancel();
        args.Reason = ent.Comp.DenyPopup;
    }

    private void OnUseInHand(Entity<STSynthItemRestrictionComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !ent.Comp.CheckUse || IsAllowed(ent.Comp, args.User))
            return;

        args.Handled = true;
        Popup(ent, args.User);
    }

    private void OnActivate(Entity<STSynthItemRestrictionComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !ent.Comp.CheckUse || IsAllowed(ent.Comp, args.User))
            return;

        args.Handled = true;
        Popup(ent, args.User);
    }

    private void OnAfterInteract(Entity<STSynthItemRestrictionComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !ent.Comp.CheckUse || IsAllowed(ent.Comp, args.User))
            return;

        args.Handled = true;
        Popup(ent, args.User);
    }

    private void OnBeforeRangedInteract(Entity<STSynthItemRestrictionComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !ent.Comp.CheckUse || IsAllowed(ent.Comp, args.User))
            return;

        args.Handled = true;
        Popup(ent, args.User);
    }

    private bool IsAllowed(STSynthItemRestrictionComponent comp, EntityUid user)
    {
        return comp.SynthOnly == HasComp<SynthComponent>(user);
    }

    private void Popup(Entity<STSynthItemRestrictionComponent> ent, EntityUid user)
    {
        _popup.PopupClient(Loc.GetString(ent.Comp.DenyPopup, ("item", ent.Owner), ("user", user)), user, user, PopupType.SmallCaution);
    }
}
