using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Sweep;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.TailSweep;

public sealed class BulwarkTailSweepSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    private static readonly ProtoId<TagPrototype> GrenadeTag = "Grenade";

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<EntityUid> _grenades = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BulwarkTailSweepComponent, BulwarkTailSweepActionEvent>(OnTailSweepAction);
    }

    private void OnTailSweepAction(Entity<BulwarkTailSweepComponent> xeno, ref BulwarkTailSweepActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        EnsureComp<XenoSweepingComponent>(xeno);
        _audio.PlayPredicted(xeno.Comp.SwingSound, xeno, xeno);

        var origin = _transform.GetMapCoordinates(xeno);

        _grenades.Clear();
        foreach (var nearby in _entityLookup.GetEntitiesInRange(Transform(xeno).Coordinates, xeno.Comp.Range))
        {
            if (_tag.HasTag(nearby, GrenadeTag))
                _grenades.Add(nearby);
        }

        _mobs.Clear();
        _entityLookup.GetEntitiesInRange(Transform(xeno).Coordinates, xeno.Comp.Range, _mobs);

        var hitEnemy = false;
        foreach (var mob in _mobs)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, mob))
                continue;

            hitEnemy = true;
            _rmcPulling.TryStopAllPullsFromAndOn(mob);

            var damage = new DamageSpecifier(
                _proto.Index<DamageTypePrototype>("Slash"),
                FixedPoint2.New(xeno.Comp.Damage));

            var dealt = _damageable.TryChangeDamage(mob, damage, true, origin: xeno);
            if (dealt?.GetTotal() > FixedPoint2.Zero)
            {
                var filter = Filter.Pvs(mob, entityManager: EntityManager)
                    .RemoveWhereAttachedEntity(o => o == xeno.Owner);
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { mob.Owner }, filter);
                _audio.PlayPredicted(xeno.Comp.HitSound, xeno, xeno);

                if (_net.IsServer)
                    SpawnAttachedTo(xeno.Comp.Effect, mob.Owner.ToCoordinates());
            }

            _stun.TryParalyze(mob, xeno.Comp.KnockdownTime, true);
            _sizeStun.KnockBack(mob, origin, 1f, 1f, 5, true);
        }

        var hitGrenade = false;
        foreach (var grenade in _grenades)
        {
            hitGrenade = true;
            var grenadeCoords = _transform.GetMapCoordinates(grenade);
            var diff = grenadeCoords.Position - origin.Position;
            var direction = diff.Length() > 0 ? diff.Normalized() : Vector2.UnitX;
            _throwing.TryThrow(grenade, direction * xeno.Comp.GrenadeKickRange, 10f);
            _audio.PlayPredicted(xeno.Comp.GrenadeKickSound, xeno, xeno);
            _popup.PopupClient(Loc.GetString("st-xeno-bulwark-tail-sweep-grenade"), xeno, xeno, PopupType.Medium);
        }

        if (hitGrenade && !hitEnemy)
        {
            foreach (var (actionId, _) in _actions.GetActions(xeno))
            {
                var ev = _actions.GetEvent(actionId);
                if (ev is BulwarkTailSweepActionEvent)
                    _actions.SetCooldown(actionId, xeno.Comp.GrenadeCooldown);
            }
        }
    }
}
