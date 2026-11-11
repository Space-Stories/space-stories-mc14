using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.XenoBoxer.BoxerPunch;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;

public sealed class BoxerJabSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedBoxerKnockoutSystem _knockout = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BoxerJabComponent, BoxerJabActionEvent>(OnJabAction);
    }

    private void OnJabAction(Entity<BoxerJabComponent> xeno, ref BoxerJabActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        if (!_interaction.InRangeUnobstructed(xeno.Owner, args.Target, 1.5f))
            return;

        args.Handled = true;

        if (!TryComp<XenoBoxerKnockoutComponent>(xeno, out var knockoutComp))
            return;

        var comp = xeno.Comp;

        _rmcMelee.DoLunge(xeno, args.Target);
        _slow.TryRoot(args.Target, comp.RootTime);

        var messageSelf = Loc.GetString("stories-xeno-boxer-jab-self-message",
            ("target", Identity.Entity(args.Target, EntityManager)));

        var messageOther = Loc.GetString("stories-xeno-boxer-jab-other-message",
            ("target", Identity.Entity(args.Target, EntityManager)), ("boxer", Identity.Entity(xeno, EntityManager)));

        _popup.PopupPredicted(messageSelf, messageOther, xeno, xeno);

        if (_net.IsServer)
            SpawnAttachedTo(comp.RootEffect, args.Target.ToCoordinates());

        _audio.PlayPredicted(comp.Sound, xeno, xeno);

        _knockout.UpdateKnockoutTracker(xeno, knockoutComp, args.Target, comp.KnockoutIncrease);

        if (!TryComp<XenoBoxerKnockoutRecentlyComponent>(xeno, out var recently))
            return;

        var tracker = recently.Trackers.GetValueOrDefault(args.Target);

        foreach (var (actionId, action) in _actions.GetActions(xeno))
        {
            var actionEvent = _actions.GetEvent(actionId);
            if (actionEvent is BoxerPunchActionEvent && tracker.Count != knockoutComp.MaxKnockout)
                _actions.SetCooldown(actionId, comp.Cooldown);
        }
    }
}
