using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;

namespace Content.Shared._Stories.Weapons.Melee.TelescopicBaton;

public sealed class STTelescopicBatonModeSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<STTelescopicBatonModeComponent, UniqueActionEvent>(OnUniqueAction);
        SubscribeLocalEvent<STTelescopicBatonModeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<STTelescopicBatonModeComponent> ent, ref MapInitEvent args)
    {
        ApplyMode(ent);
    }

    private void OnUniqueAction(Entity<STTelescopicBatonModeComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.StunMode = !ent.Comp.StunMode;
        Dirty(ent);

        ApplyMode(ent);

        var msg = Loc.GetString(ent.Comp.StunMode
            ? "st-telescopic-baton-mode-stun"
            : "st-telescopic-baton-mode-combat");
        _popup.PopupClient(msg, args.UserUid, args.UserUid, PopupType.Medium);

        args.Handled = true;
    }

    private void ApplyMode(Entity<STTelescopicBatonModeComponent> ent)
    {
        if (TryComp(ent, out ItemToggleMeleeWeaponComponent? toggleMelee))
        {
            toggleMelee.ActivatedDamage = ent.Comp.StunMode ? new() : ent.Comp.CombatDamage;
            Dirty(ent, toggleMelee);

            if (_toggle.IsActivated(ent.Owner) &&
                toggleMelee.ActivatedDamage != null &&
                TryComp(ent, out MeleeWeaponComponent? melee))
            {
                melee.Damage = toggleMelee.ActivatedDamage;
                DirtyField(ent, melee, nameof(MeleeWeaponComponent.Damage));
            }
        }

        if (ent.Comp.StunMode)
        {
            var stamina = EnsureComp<RMCStaminaDamageOnHitComponent>(ent);
            stamina.Damage = ent.Comp.StunStaminaDamage;
            Dirty(ent, stamina);

            var stun = EnsureComp<StunOnHitComponent>(ent);
            stun.Duration = ent.Comp.StunDuration;
            stun.Whitelist = ent.Comp.StunWhitelist;
            Dirty(ent, stun);
        }
        else
        {
            RemCompDeferred<RMCStaminaDamageOnHitComponent>(ent);
            RemCompDeferred<StunOnHitComponent>(ent);
        }
    }
}
