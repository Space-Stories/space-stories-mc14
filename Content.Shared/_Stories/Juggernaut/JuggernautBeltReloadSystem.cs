using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Juggernaut;

public sealed class JuggernautBeltReloadSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<JuggernautBeltReloadComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<JuggernautBeltReloadComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<JuggernautBeltReloadComponent, JuggernautBeltReloadDoAfterEvent>(OnReloadDoAfter);
        SubscribeLocalEvent<JuggernautBeltAssistedReloadComponent, AfterInteractEvent>(OnBeltAssistedAfterInteract);
    }

    private void OnInsertAttempt(Entity<JuggernautBeltReloadComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID != ent.Comp.Slot || ent.Comp.CompletingInsert)
            return;

        // Also fires from can-insert probes (verb menu) - no popups/DoAfters here.
        args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<JuggernautBeltReloadComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.Slot, out var slot))
            return;

        if (_whitelist.IsWhitelistFail(slot.Whitelist, args.Used))
            return;

        args.Handled = true;

        var delay = HasNearbySquire(args.User, ent.Comp.SquireRange) ? ent.Comp.SquireDelay : ent.Comp.Delay;
        TryStartReload(ent, args.User, args.Used, delay);
    }

    private void OnBeltAssistedAfterInteract(Entity<JuggernautBeltAssistedReloadComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<SquireWhitelistComponent>(args.User))
            return;

        foreach (var held in _hands.EnumerateHeld(target))
        {
            if (!TryComp(held, out JuggernautBeltReloadComponent? beltReload))
                continue;

            if (!_itemSlots.TryGetSlot(held, beltReload.Slot, out var slot))
                continue;

            if (_whitelist.IsWhitelistFail(slot.Whitelist, ent.Owner))
                continue;

            args.Handled = true;
            TryStartReload((held, beltReload), args.User, ent.Owner, beltReload.AssistedDelay);
            return;
        }
    }

    private void TryStartReload(Entity<JuggernautBeltReloadComponent> gun, EntityUid user, EntityUid belt, TimeSpan delay)
    {
        if (!HasComp<JuggernautWhitelistComponent>(user) && !HasComp<SquireWhitelistComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("st-juggernaut-reload-not-authorized"), user, user, PopupType.SmallCaution);
            return;
        }

        var ev = new JuggernautBeltReloadDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, ev, gun, gun, used: belt)
        {
            NeedHand = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnReloadDoAfter(Entity<JuggernautBeltReloadComponent> ent, ref JuggernautBeltReloadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        args.Handled = true;

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.Slot, out var slot))
            return;

        ent.Comp.CompletingInsert = true;
        try
        {
            // TryInsert refuses an occupied slot - eject first.
            if (slot.HasItem)
                _itemSlots.TryEjectToHands(ent, slot, args.User, excludeUserAudio: true);

            _itemSlots.TryInsert(ent, slot, used, args.User, excludeUserAudio: true);
        }
        finally
        {
            ent.Comp.CompletingInsert = false;
        }
    }

    private bool HasNearbySquire(EntityUid user, float range)
    {
        var coordinates = Transform(user).Coordinates;
        foreach (var _ in _lookup.GetEntitiesInRange<SquireWhitelistComponent>(coordinates, range))
            return true;

        return false;
    }
}

[Serializable, NetSerializable]
public sealed partial class JuggernautBeltReloadDoAfterEvent : SimpleDoAfterEvent;
