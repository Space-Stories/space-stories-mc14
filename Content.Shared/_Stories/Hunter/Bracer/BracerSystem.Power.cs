using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Stealth;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Rounding;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void InitializePower()
    {
    }

    private void UpdatePower(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<HunterBracerComponent, ActiveHunterBracerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bracer, out _, out var xform))
        {
            var wearer = xform.ParentUid;
            if (!wearer.IsValid())
                continue;

            if (bracer.Charge < bracer.MaxCharge)
            {
                var regenRate = bracer.RegenRate;
                var mapUid = xform.MapUid;
                if (
                    mapUid.HasValue
                    && (HasComp<AlmayerComponent>(mapUid.Value) || HasComp<RMCPlanetComponent>(mapUid.Value))
                )
                    regenRate = bracer.ReducedRegenRate;

                bracer.Charge = MathF.Min(bracer.MaxCharge, bracer.Charge + regenRate * frameTime);
                Dirty(uid, bracer);
            }

            SetBracerPowerAlert(wearer, (uid, bracer));
        }
    }

    private void OnBracerEquippedPower(Entity<HunterBracerComponent> ent, ref GotEquippedEvent args)
    {
        if (_net.IsServer)
        {
            EnsureComp<ActiveHunterBracerComponent>(ent);

            var comp = EnsureComp<EntityTurnInvisibleComponent>(args.Equipee);
            comp.RestrictWeapons = ent.Comp.RestrictWeaponsOnCloak;
            comp.UncloakWeaponLock = ent.Comp.UncloakWeaponLockDuration;
            Dirty(args.Equipee, comp);
        }

        SetBracerPowerAlert(args.Equipee, ent);
    }

    public bool TryDrainPower(EntityUid user, Entity<HunterBracerComponent> bracer, float? amount = null)
    {
        var cost = amount ?? bracer.Comp.PowerCost;
        if (bracer.Comp.Charge < cost)
        {
            _popup.PopupClient(Loc.GetString("st-bracer-no-power"), user, user);
            return false;
        }

        if (!_net.IsClient)
        {
            bracer.Comp.Charge -= cost;
            Dirty(bracer.Owner, bracer.Comp);
            SetBracerPowerAlert(user, bracer);
        }

        return true;
    }

    public bool TryAddBracerCharge(EntityUid wearer, EntityUid bracerUid, float amount)
    {
        if (!TryComp<HunterBracerComponent>(bracerUid, out var bracer))
            return false;

        bracer.Charge = MathF.Min(bracer.MaxCharge, bracer.Charge + amount);
        Dirty(bracerUid, bracer);
        SetBracerPowerAlert(wearer, (bracerUid, bracer));
        return true;
    }

    public void SetBracerPowerAlert(EntityUid wearer, Entity<HunterBracerComponent> bracer)
    {
        if (bracer.Comp.MaxCharge <= 0)
        {
            _alerts.ClearAlert(wearer, bracer.Comp.BracerPowerAlert);
            return;
        }

        var charge = MathF.Max(0f, bracer.Comp.Charge);
        var severity = ContentHelpers.RoundToLevels(charge, bracer.Comp.MaxCharge, 10);
        var message = Loc.GetString("stories-alerts-bracer-power-charge",
            ("charge", (int) MathF.Floor(charge / 10f) * 10),
            ("max", (int) bracer.Comp.MaxCharge));

        _alerts.ShowAlert(wearer, bracer.Comp.BracerPowerAlert, (short)severity, dynamicMessage: message);
    }
}
