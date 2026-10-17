using Content.Shared._RMC14.Armor;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Stories.Juggernaut;

public sealed class JuggernautArmorRestrictSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<JuggernautArmorRestrictComponent, BeingEquippedAttemptEvent>(OnEquippedAttempt);
        SubscribeLocalEvent<JuggernautArmorRestrictComponent, InventoryRelayedEvent<RMCEquipAttemptEvent>>(OnBlockedSlotEquipAttempt);
    }

    private void OnEquippedAttempt(Entity<JuggernautArmorRestrictComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var slots = _inventory.GetSlotEnumerator(args.EquipTarget, ent.Comp.Slots);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity == null)
                continue;

            args.Reason = ent.Comp.BlockSelfPopup;
            args.Cancel();
            return;
        }
    }

    private void OnBlockedSlotEquipAttempt(Entity<JuggernautArmorRestrictComponent> ent, ref InventoryRelayedEvent<RMCEquipAttemptEvent> args)
    {
        ref readonly var ev = ref args.Args.Event;
        if (ev.Cancelled)
            return;

        if ((ev.SlotFlags & ent.Comp.Slots) == 0)
            return;

        ev.Cancel();
        ev.Reason = ent.Comp.BlockOthersPopup;
    }
}
