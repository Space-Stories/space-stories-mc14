using System.Numerics;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Animation;
using Content.Shared._Stories.Xenonids.WarriorBulwark.EncasedPlates;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.PlateBash;

public sealed class PlateBashSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ThrownItemSystem _thrownItem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoAnimationsSystem _xenoAnimations = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownItemQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownItemQuery = GetEntityQuery<ThrownItemComponent>();

        SubscribeLocalEvent<PlateBashComponent, PlateBashActionEvent>(OnPlateBashAction);
        SubscribeLocalEvent<PlateBashComponent, ThrowDoHitEvent>(OnPlateBashHit);
        SubscribeLocalEvent<PlateBashComponent, LandEvent>(OnPlateBashLand);
        SubscribeLocalEvent<PlateBashComponent, ComponentShutdown>(OnPlateBashShutdown);
    }

    private void OnPlateBashShutdown(Entity<PlateBashComponent> xeno, ref ComponentShutdown args)
    {
        xeno.Comp.IsCharging = false;
        xeno.Comp.Target = null;
        xeno.Comp.Charge = null;
    }

    private void OnPlateBashLand(Entity<PlateBashComponent> xeno, ref LandEvent args)
    {
        if (!xeno.Comp.IsCharging)
            return;

        xeno.Comp.IsCharging = false;
        xeno.Comp.Target = null;
        xeno.Comp.Charge = null;
        Dirty(xeno);
    }

    private void OnPlateBashAction(Entity<PlateBashComponent> xeno, ref PlateBashActionEvent args)
    {
        if (args.Handled)
            return;

        var hasEncasedPlates = TryComp<EncasedPlatesComponent>(xeno, out var encased) && encased.Active;
        var target = args.Target;

        if (!_xeno.CanAbilityAttackTarget(xeno, target))
            return;

        var origin = _transform.GetMapCoordinates(xeno);
        var targetCoords = _transform.GetMapCoordinates(target);
        var diff = targetCoords.Position - origin.Position;
        var distance = diff.Length();
        var normalized = diff.Normalized();

        _rmcPulling.TryStopAllPullsFromAndOn(xeno);

        if (!hasEncasedPlates)
        {
            args.Handled = true;
            xeno.Comp.Target = target;
            xeno.Comp.IsCharging = true;
            xeno.Comp.Charge = normalized * xeno.Comp.RangeNormal;
            Dirty(xeno);

            if (_timing.IsFirstTimePredicted)
                _xenoAnimations.PlayLungeAnimationEvent(xeno, normalized * xeno.Comp.RangeNormal);

            _throwing.TryThrow(xeno, normalized * xeno.Comp.RangeNormal);
        }
        else
        {
            if (distance > xeno.Comp.RangeEncased)
                return;

            args.Handled = true;
            DealDamage(xeno, target);
            _rmcPulling.TryStopAllPullsFromAndOn(target);
            _stun.TryParalyze(target, xeno.Comp.KnockdownTime, true);
            _sizeStun.KnockBack(target, origin, xeno.Comp.KnockbackEncased, xeno.Comp.KnockbackEncased, 10, true);

            if (_net.IsServer)
                SpawnAttachedTo(xeno.Comp.Effect, target.ToCoordinates());
            _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);
        }
    }

    private void OnPlateBashHit(Entity<PlateBashComponent> xeno, ref ThrowDoHitEvent args)
    {
        if (!xeno.Comp.IsCharging)
            return;

        var target = args.Target;
        if (!_xeno.CanAbilityAttackTarget(xeno, target))
            return;

        if (target != xeno.Comp.Target)
            return;

        if (_physicsQuery.TryGetComponent(xeno, out var physics) &&
            _thrownItemQuery.TryGetComponent(xeno, out var thrown))
        {
            _thrownItem.LandComponent(xeno, thrown, physics, true);
            _thrownItem.StopThrow(xeno, thrown);
        }

        xeno.Comp.IsCharging = false;
        xeno.Comp.Target = null;
        xeno.Comp.Charge = null;
        Dirty(xeno);

        DealDamage(xeno, target);
        _rmcPulling.TryStopAllPullsFromAndOn(target);
        _stun.TryParalyze(target, xeno.Comp.KnockdownTime, true);

        var origin = _transform.GetMapCoordinates(xeno);
        _sizeStun.KnockBack(target, origin, xeno.Comp.KnockbackNormal, xeno.Comp.KnockbackNormal, 10, true);

        if (_net.IsServer)
            SpawnAttachedTo(xeno.Comp.Effect, target.ToCoordinates());
        _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);
    }

    private void DealDamage(Entity<PlateBashComponent> xeno, EntityUid target)
    {
        var damage = new DamageSpecifier(
            _proto.Index<DamageTypePrototype>("Blunt"),
            FixedPoint2.New(xeno.Comp.Damage));

        var dealt = _damageable.TryChangeDamage(target, damage, true, origin: xeno);
        if (dealt?.GetTotal() > FixedPoint2.Zero)
        {
            var filter = Filter.Pvs(target, entityManager: EntityManager)
                .RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
        }
    }
}
