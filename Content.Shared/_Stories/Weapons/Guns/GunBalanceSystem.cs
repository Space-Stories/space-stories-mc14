using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Stories.Weapons.Guns;

public sealed class GunBalanceSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, ItemSlotInsertAttemptEvent>(OnMagazineInsertAttempt);
        SubscribeLocalEvent<GunComponent, ItemSlotEjectAttemptEvent>(OnMagazineEjectAttempt);
        SubscribeLocalEvent<RMCAmmoEjectComponent, RMCTryAmmoEjectEvent>(OnAmmoEjectAttempt,
            before: [typeof(CMGunSystem), typeof(BreechLoadedSystem)]);
    }

    private void OnMagazineInsertAttempt(Entity<GunComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != SharedGunSystem.MagazineSlot)
            return;

        if (args.User is { } user && !_gun.IsGunInHands(user, ent.Owner))
            args.Cancelled = true;
    }

    private void OnMagazineEjectAttempt(Entity<GunComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID != SharedGunSystem.MagazineSlot)
            return;

        if (args.User is { } user && !_gun.IsGunInHands(user, ent.Owner))
            args.Cancelled = true;
    }

    private void OnAmmoEjectAttempt(Entity<RMCAmmoEjectComponent> ent, ref RMCTryAmmoEjectEvent args)
    {
        if (args.Cancelled || !HasComp<GunComponent>(ent))
            return;

        if (!_gun.IsGunInHands(args.User, ent.Owner))
            args.Cancelled = true;
    }
}
