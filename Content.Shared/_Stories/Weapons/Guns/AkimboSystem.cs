using System.Linq;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Weapons.Guns;

public sealed class AkimboSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AkimboComponent, GotEquippedHandEvent>(OnAkimboEquippedHand);
        SubscribeLocalEvent<AkimboComponent, GotUnequippedHandEvent>(OnAkimboUnequippedHand);
        SubscribeLocalEvent<AkimboComponent, GetWeaponAccuracyEvent>(OnAkimboGetWeaponAccuracy);
        SubscribeLocalEvent<AkimboComponent, GunRefreshModifiersEvent>(OnAkimboRefreshModifiers);
        SubscribeLocalEvent<AkimboUserComponent, ShotAttemptedEvent>(OnAkimboShotAttempted);
    }

    private void OnAkimboEquippedHand(Entity<AkimboComponent> gun, ref GotEquippedHandEvent args)
    {
        RefreshAkimboModifiers(gun);
    }

    private void OnAkimboUnequippedHand(Entity<AkimboComponent> gun, ref GotUnequippedHandEvent args)
    {
        RefreshAkimboModifiers(gun);
    }

    private void RefreshAkimboModifiers(Entity<AkimboComponent> gun)
    {
        _gun.RefreshModifiers(gun.Owner);
        if (TryGetOtherAkimboGun(gun, out var otherGun))
            _gun.RefreshModifiers(otherGun.Owner);
    }

    private void OnAkimboShotAttempted(Entity<AkimboUserComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled || !HasComp<AkimboComponent>(args.Used))
            return;

        if (!TryGetAkimboGuns(args.Used.Owner, out _, out _))
            return;

        if (ent.Comp.NextFire > _timing.CurTime)
            args.Cancel();
    }

    private void OnAkimboGetWeaponAccuracy(Entity<AkimboComponent> gun, ref GetWeaponAccuracyEvent args)
    {
        if (!TryGetOtherAkimboGun(gun, out _))
            return;

        args.AccuracyMultiplier *= gun.Comp.AccuracyMultiplier;
    }

    private void OnAkimboRefreshModifiers(Entity<AkimboComponent> gun, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetOtherAkimboGun(gun, out _))
            return;

        args.CameraRecoilScalar += gun.Comp.RecoilModifier;
    }

    public bool TryGetOtherAkimboGun(Entity<AkimboComponent> gun, out Entity<AkimboComponent> other)
    {
        other = default;
        var parent = Transform(gun.Owner).ParentUid;
        if (!parent.IsValid() || !TryComp<HandsComponent>(parent, out var handsComp))
            return false;

        foreach (var hand in handsComp.Hands.Keys)
        {
            if (_hands.GetHeldItem(parent, hand) is { } held &&
                held != gun.Owner &&
                TryComp<AkimboComponent>(held, out var otherComp))
            {
                other = (held, otherComp);
                return true;
            }
        }

        return false;
    }

    public bool TryGetAkimboGuns(EntityUid gunUid, out Entity<AkimboComponent> akimboGun, out Entity<AkimboComponent> otherAkimboGun)
    {
        akimboGun = default;
        otherAkimboGun = default;

        if (!TryComp(gunUid, out AkimboComponent? akimboComp))
            return false;

        akimboGun = (gunUid, akimboComp);
        return TryGetOtherAkimboGun(akimboGun, out otherAkimboGun);
    }

    public bool HasShootableAmmo(EntityUid gunUid)
    {
        if (TryComp<RevolverAmmoProviderComponent>(gunUid, out var revolver))
        {
            return revolver.CurrentIndex < revolver.AmmoSlots.Count &&
                   revolver.AmmoSlots.Any(slot => slot != null &&
                       (!TryComp<CartridgeAmmoComponent>(slot, out var c) || !c.Spent));
        }

        if (TryComp<MagazineAmmoProviderComponent>(gunUid, out _))
        {
            if (_containers.TryGetContainer(gunUid, "gun_chamber", out var chamberContainer))
            {
                foreach (var ent in chamberContainer.ContainedEntities)
                {
                    if (!TryComp<CartridgeAmmoComponent>(ent, out var c) || !c.Spent)
                        return true;
                }
            }

            if (_itemSlots.GetItemOrNull(gunUid, "gun_magazine") is { } magEnt)
            {
                if (TryComp<BallisticAmmoProviderComponent>(magEnt, out var ballistic))
                {
                    if (ballistic.UnspawnedCount > 0)
                        return true;

                    foreach (var ent in ballistic.Entities)
                    {
                        if (!TryComp<CartridgeAmmoComponent>(ent, out var c) || !c.Spent)
                            return true;
                    }
                }
            }

            return false;
        }

        if (TryComp<BallisticAmmoProviderComponent>(gunUid, out var ballisticGun))
        {
            if (ballisticGun.UnspawnedCount > 0)
                return true;

            foreach (var ent in ballisticGun.Entities)
            {
                if (!TryComp<CartridgeAmmoComponent>(ent, out var c) || !c.Spent)
                    return true;
            }

            return false;
        }

        if (TryComp<ChamberMagazineAmmoProviderComponent>(gunUid, out _))
        {
            if (_itemSlots.GetItemOrNull(gunUid, "gun_chamber") is { } chamberEnt &&
                (!TryComp<CartridgeAmmoComponent>(chamberEnt, out var c) || !c.Spent))
            {
                return true;
            }

            if (_itemSlots.GetItemOrNull(gunUid, "gun_magazine") is { } magEntity)
            {
                var ev = new GetAmmoCountEvent();
                RaiseLocalEvent(magEntity, ref ev);
                if (ev.Count > 0)
                    return true;
            }

            return false;
        }

        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(gunUid, ref ammoEv);
        return ammoEv.Count > 0;
    }

    public (EntityUid Entity, GunComponent Gun) SelectAkimboGun(EntityUid user, EntityUid held, GunComponent gun)
    {
        if (TryGetAkimboGuns(held, out _, out var otherAkimbo) &&
            TryComp<GunComponent>(otherAkimbo.Owner, out var otherGunComp))
        {
            var curTime = _timing.CurTime;
            var activeReady = gun.NextFire <= curTime;
            var otherReady = otherGunComp.NextFire <= curTime;
            var activeHasAmmo = HasShootableAmmo(held);
            var otherHasAmmo = HasShootableAmmo(otherAkimbo.Owner);

            if (activeHasAmmo && !otherHasAmmo)
            {
                return (held, gun);
            }
            if (!activeHasAmmo && otherHasAmmo)
            {
                return (otherAkimbo.Owner, otherGunComp);
            }
            if (activeHasAmmo && otherHasAmmo)
            {
                if (!activeReady && otherReady)
                {
                    return (otherAkimbo.Owner, otherGunComp);
                }
                if (activeReady && otherReady && TryComp<AkimboUserComponent>(user, out var shooterComp))
                {
                    if (shooterComp.LastFiredGun == held)
                    {
                        return (otherAkimbo.Owner, otherGunComp);
                    }
                }
            }
        }

        return (held, gun);

    }

    public void ApplyAkimboShootDelay(EntityUid gunUid, GunComponent gun, EntityUid user, TimeSpan curTime)
    {
        if (!TryGetAkimboGuns(gunUid, out var akimbo, out _))
            return;

        var shooter = EnsureComp<AkimboUserComponent>(user);
        shooter.LastFiredGun = gunUid;

        var baseDelay = 1f / Math.Max(gun.FireRateModified, 0.1f);
        var gunDelay = TimeSpan.FromSeconds(baseDelay / akimbo.Comp.IndividualFireRateMultiplier);
        var akimboDelay = TimeSpan.FromSeconds(baseDelay / akimbo.Comp.TotalFireRateMultiplier);

        gun.NextFire = curTime + gunDelay;
        shooter.NextFire = curTime + akimboDelay;
        DirtyField(gunUid, gun, nameof(GunComponent.NextFire));

        Dirty(user, shooter);
    }
}
