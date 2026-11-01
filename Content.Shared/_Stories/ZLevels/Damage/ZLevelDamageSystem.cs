using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Stunnable;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.ZLevels.Damage;

public sealed partial class ZLevelDamageSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    public float BaseFallingDamage { get; private set; }
    public float BaseFallingOtherDamage { get; private set; }
    public float BaseFallingStunTime { get; private set; }
    public float BaseFallingOtherStunTime { get; private set; }

    private static readonly ProtoId<DamageTypePrototype> PhysicalDamageType = "Blunt";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhysicsComponent, ZLevelHitEvent>(OnFallDamage);

        _config.OnValueChanged(CCVars.BaseFallingDamage, i => BaseFallingDamage = i, true);
        _config.OnValueChanged(CCVars.BaseFallingOtherDamage, i => BaseFallingOtherDamage = i, true);
        _config.OnValueChanged(CCVars.BaseFallingStunTime, i => BaseFallingStunTime = i, true);
        _config.OnValueChanged(CCVars.BaseFallingOtherStunTime, i => BaseFallingOtherStunTime = i, true);
    }

    private void OnFallDamage(Entity<PhysicsComponent> ent, ref ZLevelHitEvent args)
    {
        if (!_proto.TryIndex(PhysicalDamageType, out var damageType))
            return;

        var damageModifier = 1f;
        var stunModifier = 1f;

        var damageToOtherEv = new ZFallingOnTargetDamageCalculateEvent(args.ImpactPower);
        RaiseLocalEvent(ent, damageToOtherEv);
        var otherDamage = damageToOtherEv.DamageMultiplier * BaseFallingOtherDamage * args.ImpactPower *
                          args.ImpactPower;
        var otherStun = 0f;

        if (TryComp<ZFallStunOnHitComponent>(ent, out var fallStunComp))
        {
            if (args.ImpactPower >= fallStunComp.MinSpeed)
            {
                otherStun = fallStunComp.StunMultiplier * damageToOtherEv.StunMultiplier * BaseFallingOtherStunTime * args.ImpactPower * args.ImpactPower;
            }
        }
        else if (HasComp<Content.Shared.Mobs.Components.MobStateComponent>(ent))
        {
            otherStun = damageToOtherEv.StunMultiplier * BaseFallingOtherStunTime * args.ImpactPower * args.ImpactPower;
        }

        // Calculate damage modifiers for the falling entity
        var damageToSelfEv = new ZFallingDamageCalculateEvent(ent, args.ImpactPower);
        RaiseLocalEvent(ent, damageToSelfEv);
        damageModifier *= damageToSelfEv.DamageMultiplier;
        stunModifier *= damageToSelfEv.StunMultiplier;

        var entitiesAround = _lookup.GetEntitiesInRange(ent, 0.25f, LookupFlags.Uncontained);
        entitiesAround.Remove(ent); //Don't count self

        //Process entities we fell into
        var imFallOnEv = new ZImFallOnEvent(entitiesAround, args.ImpactPower);
        RaiseLocalEvent(ent, imFallOnEv);

        var victimDamageModifier = 1f;
        var victimStunModifier = 1f;

        foreach (var victim in entitiesAround)
        {
            // Calculate damage modifiers from entities being fallen upon
            var editDamageToSelfEv = new ZFallingDamageCalculateEvent(ent, args.ImpactPower);
            RaiseLocalEvent(victim, editDamageToSelfEv);
            // Most significant modifier (furthest from 1.0) wins across all victims
            if (MathF.Abs(editDamageToSelfEv.DamageMultiplier - 1f) > MathF.Abs(victimDamageModifier - 1f))
                victimDamageModifier = editDamageToSelfEv.DamageMultiplier;
            if (MathF.Abs(editDamageToSelfEv.StunMultiplier - 1f) > MathF.Abs(victimStunModifier - 1f))
                victimStunModifier = editDamageToSelfEv.StunMultiplier;

            var fellOnMeEv = new ZFellOnMeEvent(ent, args.ImpactPower);
            RaiseLocalEvent(victim, fellOnMeEv);

            // Apply damage and stun to entities that were fallen upon
            if (otherStun > 0)
            {
                _stun.TryKnockdown(victim, TimeSpan.FromSeconds(otherStun), true);
                _stun.TryStun(victim, TimeSpan.FromSeconds(otherStun), true);
            }
            if (otherDamage > 0)
            {
                var otherDmgSpec = new DamageSpecifier(damageType, otherDamage);
                _damageable.TryChangeDamage(victim, otherDmgSpec);
            }
        }

        damageModifier *= victimDamageModifier;
        stunModifier *= victimStunModifier;

        var damageAmount = MathF.Pow(args.ImpactPower, 2.2f) * BaseFallingDamage * damageModifier;
        if (damageAmount > 0)
        {
            var selfDmgSpec = new DamageSpecifier(damageType, damageAmount);
            _damageable.TryChangeDamage(ent.Owner, selfDmgSpec);
        }

        var knockdownTime = MathF.Min(args.ImpactPower * args.ImpactPower * BaseFallingStunTime * stunModifier, 5f);
        if (knockdownTime > 0)
        {
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(knockdownTime), true);
            _stun.TryStun(ent.Owner, TimeSpan.FromSeconds(knockdownTime), true);
        }
    }
}

/// <summary>
/// This event is triggered both on the entity that fell and on all entities that it fell on.
/// Together, they calculate the damage and the duration that should be applied to the fallen entity.
/// </summary>
public sealed partial class ZFallingDamageCalculateEvent(EntityUid fallen, float speed) : EntityEventArgs
{
    public EntityUid Fallen = fallen;

    public float DamageMultiplier = 1;
    public float StunMultiplier = 1;
    public float Speed = speed;
}

/// <summary>
/// Called on a falling entity to calculate how much damage it should inflict on everything it falls on.
/// </summary>
public sealed partial class ZFallingOnTargetDamageCalculateEvent(float speed) : EntityEventArgs
{
    public float DamageMultiplier = 1;
    public float StunMultiplier = 1;
    public float Speed = speed;
}

/// <summary>
/// Event raised on a falling entity to inform it about the entities it is landing on and the impact speed.
/// </summary>
public sealed partial class ZImFallOnEvent(HashSet<EntityUid> targets, float speed) : EntityEventArgs
{
    public HashSet<EntityUid> Targets = targets;
    public float Speed = speed;
}

/// <summary>
/// Event raised on an entity that is being fallen on to inform it about the falling entity and the impact speed.
/// </summary>
public sealed partial class ZFellOnMeEvent(EntityUid fallen, float speed) : EntityEventArgs
{
    public EntityUid Fallen = fallen;
    public float Speed = speed;
}
