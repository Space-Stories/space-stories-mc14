using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.TacticalMap;

public sealed class TacMapLiveUpdateSystem : EntitySystem
{
    [Dependency] private readonly XenoEvolutionSystem _evolution = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TacticalMapUserComponent, ComponentStartup>(OnUserStartup);
        SubscribeLocalEvent<GrantTacMapLiveUpdateComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<GrantTacMapLiveUpdateComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnUserStartup(Entity<TacticalMapUserComponent> ent, ref ComponentStartup args)
    {
        RefreshLiveUpdate(ent);
    }

    private void OnEquipped(Entity<GrantTacMapLiveUpdateComponent> ent, ref GotEquippedEvent args)
    {
        if ((ent.Comp.Slots & args.SlotFlags) != 0 &&
            TryComp(args.Equipee, out TacticalMapUserComponent? user))
        {
            RefreshLiveUpdate((args.Equipee, user));
        }
    }

    private void OnUnequipped(Entity<GrantTacMapLiveUpdateComponent> ent, ref GotUnequippedEvent args)
    {
        if ((ent.Comp.Slots & args.SlotFlags) != 0 &&
            TryComp(args.Equipee, out TacticalMapUserComponent? user))
        {
            RefreshLiveUpdate((args.Equipee, user));
        }
    }

    public void RefreshLiveUpdate(Entity<TacticalMapUserComponent> user)
    {
        if (_timing.ApplyingState)
            return;

        user.Comp.IntrinsicLiveUpdate ??= user.Comp.LiveUpdate;
        var live = user.Comp.IntrinsicLiveUpdate.Value;
        var slots = _inventory.GetSlotEnumerator(user.Owner);
        while (slots.NextItem(out var item, out var slot))
        {
            if (TryComp(item, out GrantTacMapLiveUpdateComponent? grant) &&
                (grant.Slots & slot.SlotFlags) != 0)
            {
                live = true;
                break;
            }
        }

        if (TryComp(user, out TacticalMapLiveUpdateOnOviComponent? ovi) && ovi.Enabled)
            live |= _evolution.HasOvipositor(user.Owner);

        if (user.Comp.LiveUpdate == live)
            return;

        user.Comp.LiveUpdate = live;
        Dirty(user);
    }
}
