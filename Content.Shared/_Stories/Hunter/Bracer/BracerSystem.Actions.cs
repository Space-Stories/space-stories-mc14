using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Language;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Hunter.Speech;
using Content.Shared.Actions;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void OnGetActions(Entity<HunterBracerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ref ent.Comp.SelfDestructAction, ent.Comp.SelfDestructActionId);
        args.AddAction(ref ent.Comp.TogglePlasmaCasterAction, ent.Comp.TogglePlasmaCasterActionId);
        args.AddAction(ref ent.Comp.CreateHealingCapsuleAction, ent.Comp.CreateHealingCapsuleActionId);
        args.AddAction(ref ent.Comp.CreateStabilizingCrystalAction, ent.Comp.CreateStabilizingCrystalActionId);
        args.AddAction(ref ent.Comp.ToggleCloakAction, ent.Comp.ToggleCloakActionId);
        args.AddAction(ref ent.Comp.ToggleTranslatorAction, ent.Comp.ToggleTranslatorActionId);

        args.AddAction(ref ent.Comp.ToggleAttachmentsAction, ent.Comp.ToggleAttachmentsActionId);

        var wearer = Transform(ent).ParentUid;
        if (wearer.IsValid())
            UpdateAttachmentActionState(ent, wearer);

        if (ent.Comp.ToggleTranslatorAction is { } translatorAction)
            _actions.SetToggled(translatorAction, ent.Comp.TranslatorActive);

        Dirty(ent);
    }

    private void OnToggleTranslator(Entity<HunterBracerComponent> ent, ref ToggleTranslatorEvent args)
    {
        if (args.Handled)
            return;

        if (!AttemptUsage(args.Performer, ent))
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;

        ent.Comp.TranslatorActive = !ent.Comp.TranslatorActive;
        Dirty(ent);

        RaiseLocalEvent(args.Performer, new HunterTranslatorToggledEvent(args.Performer, ent.Comp.TranslatorActive));

        if (ent.Comp.ToggleTranslatorAction is { } action)
            _actions.SetToggled(action, ent.Comp.TranslatorActive);

        _audio.PlayPvs(ent.Comp.TranslatorSound, args.Performer);

        var msg = ent.Comp.TranslatorActive
            ? Loc.GetString("st-bracer-translator-on")
            : Loc.GetString("st-bracer-translator-off");
        _popup.PopupEntity(msg, args.Performer, args.Performer);
    }

    private void UpdateAttachmentActionState(Entity<HunterBracerComponent> ent, EntityUid user)
    {
        if (ent.Comp.ToggleAttachmentsAction is not { } action)
            return;

        var hasAttachment =
            _itemSlots.TryGetSlot(ent, LeftAttachmentSlotId, out var left) && left.Item is not null
            || _itemSlots.TryGetSlot(ent, RightAttachmentSlotId, out var right) && right.Item is not null;

        _actions.SetEnabled(action, hasAttachment);

        if (hasAttachment)
        {
            var isDeployed =
                IsAttachmentDeployed(ent, LeftAttachmentSlotId, user)
                || IsAttachmentDeployed(ent, RightAttachmentSlotId, user);
            _actions.SetToggled(action, isDeployed);
        }
        else
        {
            if (
                IsAttachmentDeployed(ent, LeftAttachmentSlotId, user)
                || IsAttachmentDeployed(ent, RightAttachmentSlotId, user)
            )
                RetractAttachments(user, ent);
            _actions.SetToggled(action, false);
        }
    }
}

public sealed partial class RequestSelfDestructEvent : InstantActionEvent
{
}

public sealed partial class TogglePlasmaCasterEvent : InstantActionEvent
{
}

public sealed partial class CreateHealingCapsuleEvent : InstantActionEvent
{
}

public sealed partial class CreateStabilizingCrystalEvent : InstantActionEvent
{
}

public sealed partial class ToggleBracerAttachmentsEvent : InstantActionEvent
{
}

public sealed partial class ToggleBracerCloakEvent : InstantActionEvent
{
}

public sealed partial class ToggleTranslatorEvent : InstantActionEvent
{
}
