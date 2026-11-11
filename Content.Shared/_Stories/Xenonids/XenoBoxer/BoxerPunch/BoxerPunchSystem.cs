using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerPunch;

public sealed class BoxerPunchSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedBoxerKnockoutSystem _knockout = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerPunchComponent, BoxerPunchActionEvent>(OnBoxerPunchAction);
    }

    private void OnBoxerPunchAction(Entity<BoxerPunchComponent> xeno, ref BoxerPunchActionEvent args)
    {
        if (args.Handled)
            return;

        var comp = xeno.Comp;
        var targetUid = args.Target;

        if (!_xeno.CanAbilityAttackTarget(xeno, targetUid))
            return;

        if (!_interaction.InRangeUnobstructed(xeno.Owner, targetUid, 1.5f))
            return;

        args.Handled = true;

        if (!TryComp<XenoBoxerKnockoutComponent>(xeno, out var knockoutComp))
            return;

        _rmcPulling.TryStopAllPullsFromAndOn(targetUid);

        var damage = _damageable.TryChangeDamage(targetUid, comp.Damage);
        if (damage?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(targetUid, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { targetUid }, filter);
        }

        _rmcMelee.DoLunge(xeno, targetUid);
        _slow.TrySlowdown(targetUid, comp.SlowDuration);

        if (_net.IsServer)
            SpawnAttachedTo(comp.Effect, targetUid.ToCoordinates());

        _audio.PlayPredicted(comp.Sound, xeno, xeno);

        _knockout.UpdateKnockoutTracker(xeno, knockoutComp, args.Target, comp.KnockoutIncrease);
        if (!TryComp<XenoBoxerKnockoutRecentlyComponent>(xeno, out var recently))
            return;

        var tracker = recently.Trackers.GetValueOrDefault(args.Target);

        foreach (var (actionId, action) in _actions.GetActions(xeno))
        {
            var actionEvent = _actions.GetEvent(actionId);
            if (actionEvent is BoxerJabActionEvent && tracker.Count != knockoutComp.MaxKnockout)
                _actions.SetCooldown(actionId, comp.Cooldown);
        }
    }
}
