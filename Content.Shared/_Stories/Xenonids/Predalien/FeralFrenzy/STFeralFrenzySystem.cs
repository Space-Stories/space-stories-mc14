using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralFrenzy;

public sealed class STFeralFrenzySystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STFeralFrenzyComponent, STFeralFrenzyActionEvent>(OnFrenzyAction);
        SubscribeLocalEvent<STFeralFrenzyComponent, STFeralFrenzySingleDoAfterEvent>(OnSingleDoAfter);
        SubscribeLocalEvent<STFeralFrenzyComponent, STFeralFrenzyAoeDoAfterEvent>(OnAoeDoAfter);
    }

    private void OnFrenzyAction(Entity<STFeralFrenzyComponent> xeno, ref STFeralFrenzyActionEvent args)
    {
        if (args.Handled)
            return;

        if (xeno.Comp.Targeting == STFeralFrenzyTargeting.Aoe)
        {
            args.Handled = true;
            StartAoe(xeno);
            return;
        }

        if (args.Entity is not { } target)
        {
            _popup.PopupClient(Loc.GetString("st-predalien-feral-frenzy-single-need-target"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        if (!_xeno.CanAbilityAttackTarget(xeno, target))
            return;

        if (!TryComp<MobStateComponent>(target, out var targetMobState) || _mobState.IsDead(target, targetMobState))
            return;

        if (!Transform(xeno).Coordinates.TryDistance(EntityManager, Transform(target).Coordinates, out var distance) ||
            distance > xeno.Comp.SingleRange)
            return;

        args.Handled = true;
        StartSingle(xeno, target);
    }

    private void StartSingle(Entity<STFeralFrenzyComponent> xeno, EntityUid target)
    {
        _slow.TryRoot(xeno.Owner, xeno.Comp.SingleCastTime);
        _slow.TryRoot(target, xeno.Comp.SingleCastTime);

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.SingleCastTime, new STFeralFrenzySingleDoAfterEvent(), xeno, target)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _popup.PopupClient(Loc.GetString("st-predalien-feral-frenzy-single-start"), xeno, xeno, PopupType.LargeCaution);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void StartAoe(Entity<STFeralFrenzyComponent> xeno)
    {
        _slow.TryRoot(xeno.Owner, xeno.Comp.AoeCastTime);

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.AoeCastTime, new STFeralFrenzyAoeDoAfterEvent(), xeno)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _popup.PopupClient(Loc.GetString("st-predalien-feral-frenzy-aoe-start"), xeno, xeno, PopupType.LargeCaution);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnSingleDoAfter(Entity<STFeralFrenzyComponent> xeno, ref STFeralFrenzySingleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!_xeno.CanAbilityAttackTarget(xeno, target))
            return;

        if (!TryComp<MobStateComponent>(target, out var targetMobState) || _mobState.IsDead(target, targetMobState))
            return;

        args.Handled = true;

        var kills = TryComp<STPredalienComponent>(xeno.Owner, out var predalien)
            ? Math.Min(predalien.Kills, predalien.MaxKills)
            : 0;

        DealDamage(xeno, target, xeno.Comp.SingleBaseDamage + xeno.Comp.SingleDamagePerKill * kills, xeno.Comp.SingleSound);

        _popup.PopupClient(Loc.GetString("st-predalien-feral-frenzy-single-finish", ("target", target)), xeno, xeno, PopupType.LargeCaution);
    }

    private void OnAoeDoAfter(Entity<STFeralFrenzyComponent> xeno, ref STFeralFrenzyAoeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var kills = TryComp<STPredalienComponent>(xeno.Owner, out var predalien)
            ? Math.Min(predalien.Kills, predalien.MaxKills)
            : 0;

        var damage = xeno.Comp.AoeBaseDamage + xeno.Comp.AoeDamagePerKill * kills;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(xeno.Owner.ToCoordinates(), xeno.Comp.AoeRange, _nearby);

        foreach (var receiver in _nearby)
        {
            if (receiver.Owner == xeno.Owner)
                continue;

            if (_mobState.IsDead(receiver.Owner, receiver.Comp))
                continue;

            if (!_xeno.CanAbilityAttackTarget(xeno, receiver.Owner))
                continue;

            if (!_interaction.InRangeUnobstructed(xeno.Owner, receiver.Owner, xeno.Comp.AoeRange))
                continue;

            DealDamage(xeno, receiver.Owner, damage, xeno.Comp.AoeSound);
        }

        _popup.PopupClient(Loc.GetString("st-predalien-feral-frenzy-aoe-finish"), xeno, xeno, PopupType.LargeCaution);
    }

    private void DealDamage(Entity<STFeralFrenzyComponent> xeno, EntityUid target, float amount, SoundSpecifier sound)
    {
        if (_proto.TryIndex<DamageGroupPrototype>("Brute", out var brute))
        {
            var damage = new DamageSpecifier(brute, amount);
            _damageable.TryChangeDamage(target, damage, origin: xeno.Owner, tool: xeno.Owner);
        }

        _stun.TryParalyze(target, xeno.Comp.StunDuration, true);
        _audio.PlayPredicted(sound, xeno, xeno);

        if (_net.IsServer)
            SpawnAttachedTo(xeno.Comp.GoreEffect, target.ToCoordinates());
    }
}
