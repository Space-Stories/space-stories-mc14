using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerPunch;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerUppercut;

public sealed class BoxerUppercutSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedBoxerKnockoutSystem _knockout = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamage = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly RMCSizeStunSystem _rmcStun = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    
    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerUppercutComponent, BoxerUppercutActionEvent>(OnBoxerUppercutAction);
    }

    private void OnBoxerUppercutAction(Entity<BoxerUppercutComponent> xeno, ref BoxerUppercutActionEvent args)
    {
        if (args.Handled)
            return;

        var targetUid = args.Target;
        var comp = xeno.Comp;
        var popupPower = "weak";

        if (!_xeno.CanAbilityAttackTarget(xeno, targetUid))
            return;

        if (!_interaction.InRangeUnobstructed(xeno.Owner, targetUid, 1.5f))
            return;

        if (TryComp<RMCSizeComponent>(targetUid, out var size) && size.Size == RMCSizes.Immobile)
            return;

        args.Handled = true;

        if (!TryComp(xeno, out XenoBoxerKnockoutComponent? knockoutComp) ||
            !TryComp(xeno, out XenoBoxerKnockoutRecentlyComponent? recently))
            return;

        if (!_mobThreshold.TryGetDeadThreshold(xeno, out var threshold))
            return;

        var tracker = recently.Trackers.GetValueOrDefault(args.Target);
        _audio.PlayPredicted(comp.ClawSound, xeno, xeno);

        var damageModificator = Math.Min(tracker.Count * comp.DamageModificator, comp.MaxDamage);

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.GetMapCoordinates(targetUid);
        var diff = (target.Position - origin.Position).Normalized() * (tracker.Count / comp.Range);

        var damage = _damageable.TryChangeDamage(targetUid, new DamageSpecifier(
            _proto.Index<DamageTypePrototype>("Blunt"), damageModificator), true);

        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(targetUid, entityManager: EntityManager)
                .RemoveWhereAttachedEntity(o => o == xeno.Owner);

            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { targetUid }, filter);
            popupPower = "good";
        }

        var heal = threshold.Value * (Math.Clamp(tracker.Count, 0, knockoutComp.MaxKnockout) * comp.HealPerStack);
        var amount = -_rmcDamage.DistributeTypesTotal(xeno.Owner, heal);

        _damageable.TryChangeDamage(xeno, amount, true);

        if (_net.IsServer)
            SpawnAttachedTo(comp.HealEffect, xeno.Owner.ToCoordinates());

        _rmcPulling.TryStopAllPullsFromAndOn(targetUid);

        if (tracker.Count <= 5)
        {
            _throwing.TryThrow(targetUid, diff, 10);
            popupPower = "powerful";
        }
        else if (tracker.Count <= 10)
        {
            _throwing.TryThrow(targetUid, diff, 10);
            _stun.TryParalyze(targetUid, comp.ParalyzeTime, true);
            _stun.TrySlowdown(targetUid, comp.ParalyzeTime * 2, true);
            popupPower = "gigantic";
        }
        else
        {
            _throwing.TryThrow(targetUid, diff, 10);
            _rmcStun.TryKnockOut(targetUid, comp.UnconsciousTime);
            _audio.PlayLocal(comp.GongSound, xeno, xeno);

            EnsureComp<KnockoutLabelComponent>(targetUid);
            popupPower = "titanic";
        }

        if (_net.IsClient)
            SpawnAttachedTo(comp.Effect, targetUid.ToCoordinates());

        var messageOther = Loc.GetString("stories-xeno-boxer-strain-other-uppercut-" + popupPower,
            ("target", Identity.Name(targetUid, EntityManager)),
            ("boxer", Identity.Name(xeno, EntityManager)));

        var messageSelf = Loc.GetString("stories-xeno-boxer-strain-self-uppercut-" + popupPower,
            ("target", Identity.Name(targetUid, EntityManager)),
            ("boxer", Identity.Name(xeno, EntityManager)));

        _popup.PopupPredicted(messageSelf, messageOther, xeno, xeno, PopupType.LargeCaution);

        _rmcMelee.DoLunge(xeno, targetUid);

        foreach (var (actionId, action) in _actions.GetActions(xeno))
        {
            var actionEvent = _actions.GetEvent(actionId);
            if (actionEvent is BoxerPunchActionEvent or BoxerJabActionEvent)
                _actions.SetCooldown(actionId, comp.Cooldown);
        }

        _knockout.ResetTracker(xeno, recently);
    }
}
