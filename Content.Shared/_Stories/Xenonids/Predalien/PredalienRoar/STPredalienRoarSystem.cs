using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Stab;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Predalien.PredalienRoar;

public sealed class STPredalienRoarSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STPredalienRoarComponent, STPredalienRoarActionEvent>(OnRoarAction);

        SubscribeLocalEvent<STPredalienRoarBuffedComponent, ComponentShutdown>(OnBuffedShutdown);
        SubscribeLocalEvent<STPredalienRoarBuffedComponent, RefreshMovementSpeedModifiersEvent>(OnBuffedRefreshSpeed);
        SubscribeLocalEvent<STPredalienRoarBuffedComponent, GetMeleeDamageEvent>(OnBuffedGetMeleeDamage);
        SubscribeLocalEvent<STPredalienRoarBuffedComponent, RMCGetTailStabBonusDamageEvent>(OnBuffedGetTailStabDamage);
    }

    private void OnRoarAction(Entity<STPredalienRoarComponent> xeno, ref STPredalienRoarActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var kills = TryComp<STPredalienComponent>(xeno.Owner, out var predalien)
            ? Math.Min(predalien.Kills, predalien.MaxKills)
            : 0;

        _popup.PopupClient(Loc.GetString("st-predalien-roar-self"), xeno, xeno, PopupType.LargeCaution);
        _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);

        if (_net.IsServer)
            SpawnAttachedTo("CMEffectScreech", xeno.Owner.ToCoordinates());

        if (!_net.IsClient)
            AffectNearby(xeno, kills);
    }

    private void AffectNearby(Entity<STPredalienRoarComponent> xeno, int kills)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(xeno.Owner.ToCoordinates(), xeno.Comp.Radius, _nearby);

        var duration = xeno.Comp.BaseDuration + xeno.Comp.DurationPerKill * kills;
        var bonusDamage = xeno.Comp.BonusDamagePerKill * kills;
        var speedModifier = 1f + xeno.Comp.BonusSpeedPerKill * kills;

        foreach (var receiver in _nearby)
        {
            if (receiver.Owner == xeno.Owner)
                continue;

            var reveal = new STPredalienRevealEvent(receiver.Owner, xeno.Comp.HunterRecloakPenalty);
            RaiseLocalEvent(receiver.Owner, reveal, true);

            if (HasComp<HunterComponent>(receiver.Owner))
                continue;

            if (!HasComp<XenoComponent>(receiver.Owner) || !_hive.FromSameHive(xeno.Owner, receiver.Owner))
                continue;

            if (_mobState.IsDead(receiver.Owner, receiver.Comp))
                continue;

            var buff = EnsureComp<STPredalienRoarBuffedComponent>(receiver.Owner);
            buff.BonusDamage = bonusDamage;
            buff.SpeedModifier = speedModifier;
            buff.ExpireAt = _timing.CurTime + duration;
            Dirty(receiver.Owner, buff);

            _speed.RefreshMovementSpeedModifiers(receiver.Owner);
        }
    }

    private void OnBuffedShutdown(Entity<STPredalienRoarBuffedComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnBuffedRefreshSpeed(Entity<STPredalienRoarBuffedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime >= ent.Comp.ExpireAt)
            return;

        args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    private void OnBuffedGetMeleeDamage(Entity<STPredalienRoarBuffedComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (_timing.CurTime >= ent.Comp.ExpireAt)
            return;

        if (ent.Comp.BonusDamage > 0 && _proto.TryIndex<DamageGroupPrototype>("Brute", out var brute))
            args.Damage += new DamageSpecifier(brute, ent.Comp.BonusDamage);
    }

    private void OnBuffedGetTailStabDamage(Entity<STPredalienRoarBuffedComponent> ent, ref RMCGetTailStabBonusDamageEvent args)
    {
        if (_timing.CurTime >= ent.Comp.ExpireAt)
            return;

        if (ent.Comp.BonusDamage > 0 && _proto.TryIndex<DamageGroupPrototype>("Brute", out var brute))
            args.Damage += new DamageSpecifier(brute, ent.Comp.BonusDamage);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<STPredalienRoarBuffedComponent>();
        while (query.MoveNext(out var uid, out var buffed))
        {
            if (time < buffed.ExpireAt)
                continue;

            RemCompDeferred<STPredalienRoarBuffedComponent>(uid);
        }
    }
}
