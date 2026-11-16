using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._Stories.Xenonids.WarriorBulwark.ReflectiveShield;
using Content.Shared.Actions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.EncasedPlates;

public sealed class EncasedPlatesSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly CMArmorSystem _armor = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedRMCExplosionSystem _explosion = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EncasedPlatesComponent, EncasedPlatesActionEvent>(OnEncasedPlatesAction);
        SubscribeLocalEvent<EncasedPlatesComponent, CMGetArmorEvent>(OnEncasedPlatesGetArmor);
        SubscribeLocalEvent<EncasedPlatesComponent, RefreshMovementSpeedModifiersEvent>(OnEncasedPlatesRefreshSpeed);
        SubscribeLocalEvent<EncasedPlatesComponent, GetMeleeDamageEvent>(OnEncasedPlatesGetMeleeDamage);
        SubscribeLocalEvent<EncasedPlatesComponent, BeforeStatusEffectAddedEvent>(OnEncasedPlatesBeforeStatusAdded);
        SubscribeLocalEvent<EncasedPlatesComponent, XenoRestAttemptEvent>(OnEncasedPlatesRestAttempt);
        SubscribeLocalEvent<EncasedPlatesComponent, ComponentShutdown>(OnEncasedPlatesShutdown);
        SubscribeLocalEvent<EncasedPlatesComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<EncasedPlatesComponent> xeno, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Deactivate(xeno);
    }

    private void Deactivate(Entity<EncasedPlatesComponent> xeno)
    {
        if (!xeno.Comp.Active)
            return;

        xeno.Comp.Active = false;
        Dirty(xeno);

        if (TryComp<RMCSizeComponent>(xeno, out var size))
        {
            size.Size = xeno.Comp.OriginalSize ?? RMCSizes.Xeno;
            Dirty(xeno.Owner, size);
        }

        _appearance.SetData(xeno, XenoVisualLayers.EncasedPlates, false);
        _armor.UpdateArmorValue((xeno, null));
        _movementSpeed.RefreshMovementSpeedModifiers(xeno);

        if (TryComp<StunOnExplosionReceivedComponent>(xeno, out var stunExplosion))
            _explosion.ChangeExplosionStunResistance(xeno, stunExplosion, true);

        foreach (var action in _rmcActions.GetActionsWithEvent<EncasedPlatesActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), false);
        }
    }

    private void OnEncasedPlatesShutdown(Entity<EncasedPlatesComponent> xeno, ref ComponentShutdown args)
    {
        if (!xeno.Comp.Active)
            return;

        xeno.Comp.Active = false;

        if (TryComp<RMCSizeComponent>(xeno, out var size))
        {
            size.Size = xeno.Comp.OriginalSize ?? RMCSizes.Xeno;
            Dirty(xeno.Owner, size);
        }

        _appearance.SetData(xeno, XenoVisualLayers.EncasedPlates, false);
        _armor.UpdateArmorValue((xeno, null));
        _movementSpeed.RefreshMovementSpeedModifiers(xeno);

        if (TryComp<StunOnExplosionReceivedComponent>(xeno, out var stunExplosion))
            _explosion.ChangeExplosionStunResistance(xeno, stunExplosion, true);
    }

    private void OnEncasedPlatesAction(Entity<EncasedPlatesComponent> xeno, ref EncasedPlatesActionEvent args)
    {
        if (args.Handled)
            return;

        if (xeno.Comp.Active &&
            TryComp<ReflectiveShieldComponent>(xeno, out var shield) &&
            shield.Active)
        {
            _popup.PopupClient(Loc.GetString("st-xeno-bulwark-encased-plates-shield-active"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        xeno.Comp.Active = !xeno.Comp.Active;
        Dirty(xeno);

        if (TryComp<RMCSizeComponent>(xeno, out var size))
        {
            if (xeno.Comp.Active)
            {
                xeno.Comp.OriginalSize = size.Size;
                size.Size = xeno.Comp.ActiveSize;
            }
            else
            {
                size.Size = xeno.Comp.OriginalSize ?? RMCSizes.Xeno;
            }
            Dirty(xeno.Owner, size);
        }

        if (xeno.Comp.Active)
            _popup.PopupClient(Loc.GetString("st-xeno-bulwark-encased-plates-activate"), xeno, xeno, PopupType.Medium);
        else
            _popup.PopupClient(Loc.GetString("st-xeno-bulwark-encased-plates-deactivate"), xeno, xeno, PopupType.Small);

        _appearance.SetData(xeno, XenoVisualLayers.EncasedPlates, xeno.Comp.Active);
        _armor.UpdateArmorValue((xeno, null));
        _movementSpeed.RefreshMovementSpeedModifiers(xeno);

        if (TryComp<StunOnExplosionReceivedComponent>(xeno, out var stunExplosion))
            _explosion.ChangeExplosionStunResistance(xeno, stunExplosion, !xeno.Comp.Active);

        foreach (var action in _rmcActions.GetActionsWithEvent<EncasedPlatesActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), xeno.Comp.Active);
        }
    }

    private void OnEncasedPlatesGetArmor(Entity<EncasedPlatesComponent> xeno, ref CMGetArmorEvent args)
    {
        if (!xeno.Comp.Active)
            return;

        args.FrontalArmor += xeno.Comp.FrontalArmorBonus;
        args.SideArmor += xeno.Comp.SideArmorBonus;
    }

    private void OnEncasedPlatesRefreshSpeed(Entity<EncasedPlatesComponent> xeno, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (xeno.Comp.Active)
            args.ModifySpeed(xeno.Comp.SpeedMultiplier, xeno.Comp.SpeedMultiplier);
    }

    private void OnEncasedPlatesGetMeleeDamage(Entity<EncasedPlatesComponent> xeno, ref GetMeleeDamageEvent args)
    {
        if (!xeno.Comp.Active)
            return;

        var count = args.Damage.DamageDict.Count;
        if (count == 0)
            return;

        var modifierPerType = xeno.Comp.DamageModifier / count;
        foreach (var (type, _) in args.Damage.DamageDict)
        {
            args.Damage.DamageDict[type] += modifierPerType;
        }
    }

    private void OnEncasedPlatesBeforeStatusAdded(Entity<EncasedPlatesComponent> xeno, ref BeforeStatusEffectAddedEvent args)
    {
        if (xeno.Comp.Active && xeno.Comp.ImmuneToStatuses.Contains(args.Effect.Id))
            args.Cancelled = true;
    }

    private void OnEncasedPlatesRestAttempt(Entity<EncasedPlatesComponent> xeno, ref XenoRestAttemptEvent args)
    {
        if (xeno.Comp.Active)
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("st-xeno-bulwark-encased-plates-cant-rest"), xeno, xeno, PopupType.SmallCaution);
        }
    }
}
