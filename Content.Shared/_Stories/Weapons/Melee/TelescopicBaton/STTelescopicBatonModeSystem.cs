using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Weapons.Melee.TelescopicBaton;

public sealed class STTelescopicBatonModeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly RMCStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<STTelescopicBatonModeComponent, UniqueActionEvent>(OnUniqueAction);
        SubscribeLocalEvent<STTelescopicBatonModeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<STTelescopicBatonModeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<STTelescopicBatonModeComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.StunMode || !args.IsHit || !_toggle.IsActivated(ent.Owner))
            return;

        TryStunXenos(ent, args);

        var targets = new List<Entity<RMCStaminaComponent>>();
        foreach (var hit in args.HitEntities)
        {
            if (hit == args.User || !_whitelist.IsValid(ent.Comp.StunWhitelist, hit))
                continue;

            if (TryComp(hit, out RMCStaminaComponent? stamina))
                targets.Add((hit, stamina));
        }

        if (targets.Count == 0)
            return;

        var damage = ent.Comp.StunStaminaDamage;
        if (ent.Comp.StunSkillMultipliers.TryGetValue(_skills.GetSkill(args.User, ent.Comp.StunSkill), out var multiplier))
            damage *= multiplier;

        foreach (var target in targets)
        {
            _stamina.DoStaminaDamage((target, target.Comp), damage / targets.Count);
        }
    }

    private void TryStunXenos(Entity<STTelescopicBatonModeComponent> ent, MeleeHitEvent args)
    {
        if (!HasComp<SynthComponent>(args.User))
            return;

        var time = _timing.CurTime;
        if (TryComp(args.User, out STXenoStunCooldownComponent? cooldown) && time < cooldown.NextXenoStun)
            return;

        var stunned = false;
        foreach (var hit in args.HitEntities)
        {
            if (!TryComp(hit, out XenoComponent? xeno))
                continue;

            if (!ent.Comp.XenoTierStuns.TryGetValue(xeno.Tier, out var duration) || duration <= TimeSpan.Zero)
                continue;

            if (_stun.TryParalyze(hit, duration, true))
                stunned = true;
        }

        if (!stunned)
            return;

        cooldown = EnsureComp<STXenoStunCooldownComponent>(args.User);
        cooldown.NextXenoStun = time + ent.Comp.XenoStunCooldown;
        Dirty(args.User, cooldown);
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
    }
}
