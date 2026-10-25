using System.Numerics;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Random;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Stories.ProjectileCover;

public sealed class StoriesProjectileCoverSystem : EntitySystem
{
    private const float PerpendicularThreshold = 0.38f;
    private const float DirectionalEdgeOffset = 0.44f;
    private const float TileHalfSize = 0.5f;

    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly FixtureSystem _fixture = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private float _startDistance;
    private float _fullDistance;
    private float _accuracyFactor;
    private float _strayDamageMultiplier;
    private float _targetedDamageMultiplier;

    public override void Initialize()
    {
        Subs.CVar(_config, SCCVars.SCCVars.CoverStartDistance, v => _startDistance = v, true);
        Subs.CVar(_config, SCCVars.SCCVars.CoverFullDistance, v => _fullDistance = v, true);
        Subs.CVar(_config, SCCVars.SCCVars.CoverAccuracyFactor, v => _accuracyFactor = v, true);
        Subs.CVar(_config, SCCVars.SCCVars.CoverStrayDamageMultiplier, v => _strayDamageMultiplier = v, true);
        Subs.CVar(_config, SCCVars.SCCVars.CoverTargetedDamageMultiplier, v => _targetedDamageMultiplier = v, true);

        SubscribeLocalEvent<StoriesProjectileCoverComponent, ComponentStartup>(OnCoverStartup);
        SubscribeLocalEvent<StoriesProjectileCoverComponent, RMCBeforeProjectileAccuracyEvent>(OnCoverBeforeProjectileAccuracy);
        SubscribeLocalEvent<StoriesProjectileCoverComponent, DamageModifyEvent>(OnCoverDamageModify);
        SubscribeLocalEvent<StoriesProjectileCoverComponent, PreventCollideEvent>(OnCoverPreventCollide);
        SubscribeLocalEvent<StoriesProjectileCoverComponent, ThrowHitByEvent>(OnCoverThrowHitBy);
        SubscribeLocalEvent<RMCProjectileAccuracyComponent, ComponentStartup>(OnProjectileStartup);
    }

    private void OnCoverStartup(Entity<StoriesProjectileCoverComponent> ent, ref ComponentStartup args)
    {
        if (_fixture.GetFixtureOrNull(ent, ent.Comp.FixtureId) is not { } fixture)
            return;

        _physics.AddCollisionLayer(ent, ent.Comp.FixtureId, fixture, (int) CollisionGroup.ProjectileCover);
    }

    private void OnProjectileStartup(Entity<RMCProjectileAccuracyComponent> ent, ref ComponentStartup args)
    {
        if (_fixture.GetFixtureOrNull(ent, SharedProjectileSystem.ProjectileFixture) is not { } fixture)
            return;

        _physics.AddCollisionMask(ent, SharedProjectileSystem.ProjectileFixture, fixture, (int) CollisionGroup.ProjectileCover);
    }

    private void OnCoverDamageModify(Entity<StoriesProjectileCoverComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Tool is not { } tool || !HasComp<ProjectileComponent>(tool))
            return;

        var targeted = CompOrNull<TargetedProjectileComponent>(tool)?.Target == ent.Owner;
        args.Damage *= targeted ? _targetedDamageMultiplier : _strayDamageMultiplier;
    }

    private void OnCoverPreventCollide(Entity<StoriesProjectileCoverComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled ||
            !TryComp(args.OtherEntity, out ThrownItemComponent? thrown) ||
            HasComp<ProjectileComponent>(args.OtherEntity))
        {
            return;
        }

        const int coverLayer = (int) CollisionGroup.ProjectileCover;
        if ((args.OurFixture.CollisionLayer & args.OtherFixture.CollisionMask & ~coverLayer) != 0 ||
            (args.OurFixture.CollisionMask & args.OtherFixture.CollisionLayer) != 0)
        {
            return;
        }

        if (!ShouldBlockThrown(ent, args.OtherEntity, thrown))
            args.Cancelled = true;
    }

    private void OnCoverThrowHitBy(Entity<StoriesProjectileCoverComponent> ent, ref ThrowHitByEvent args)
    {
        if (!ShouldBlockThrown(ent, args.Thrown, args.Component))
            return;

        _physics.SetLinearVelocity(args.Thrown, Vector2.Zero);
    }

    private bool ShouldBlockThrown(Entity<StoriesProjectileCoverComponent> ent, EntityUid thrown, ThrownItemComponent thrownComp)
    {
        if (!ent.Comp.Directional || !ent.Comp.BlockThrown || HasComp<MobStateComponent>(thrown))
            return false;

        var velocity = _physics.GetMapLinearVelocity(thrown);
        if (velocity.LengthSquared() < 0.0001f)
            return false;

        var facing = _transform.GetWorldRotation(ent).RotateVec(new Vector2(0, -1));
        if (Vector2.Dot(Vector2.Normalize(velocity), facing) >= -PerpendicularThreshold)
            return false;

        if (thrownComp.Thrower is not { } thrower || TerminatingOrDeleted(thrower))
            return true;

        var coverPos = _transform.GetMapCoordinates(ent);
        var throwerPos = _transform.GetMapCoordinates(thrower);
        if (coverPos.MapId != throwerPos.MapId)
            return true;

        var distance = (coverPos.Position + facing * DirectionalEdgeOffset - throwerPos.Position).Length();
        return distance >= _startDistance;
    }

    private void OnCoverBeforeProjectileAccuracy(Entity<StoriesProjectileCoverComponent> ent, ref RMCBeforeProjectileAccuracyEvent args)
    {
        if (args.GuaranteedMiss)
            return;

        if (CompOrNull<TargetedProjectileComponent>(args.Projectile)?.Target == ent.Owner)
            return;

        if (HasComp<StoriesIgnoreCoverComponent>(args.Projectile))
        {
            args.GuaranteedMiss = true;
            return;
        }

        if (!TryComp(args.Projectile, out RMCProjectileAccuracyComponent? accuracy) ||
            accuracy.ShotFrom is not { } shotFromCoords)
        {
            return;
        }

        var coverPos = _transform.GetMapCoordinates(ent);
        var shotFrom = _transform.ToMapCoordinates(shotFromCoords);
        if (coverPos.MapId != shotFrom.MapId)
            return;

        var toCover = coverPos.Position - shotFrom.Position;
        var direction = _physics.GetMapLinearVelocity(args.Projectile);
        if (direction.LengthSquared() < 0.0001f)
            direction = toCover;

        direction = direction.LengthSquared() < 0.0001f ? Vector2.Zero : Vector2.Normalize(direction);

        float distance;
        if (ent.Comp.Directional)
        {
            var facing = _transform.GetWorldRotation(ent).RotateVec(new Vector2(0, -1));
            if (Vector2.Dot(direction, facing) >= -PerpendicularThreshold)
            {
                args.GuaranteedMiss = true;
                return;
            }

            if (HasComp<StoriesStoppedByCoverComponent>(args.Projectile))
                return;

            distance = (coverPos.Position + facing * DirectionalEdgeOffset - shotFrom.Position).Length();
        }
        else
        {
            if (IsShotOverCover(ent, coverPos, direction))
            {
                args.GuaranteedMiss = true;
                return;
            }

            distance = MathF.Max(0, toCover.Length() - TileHalfSize);
        }

        var ramp = _fullDistance > _startDistance
            ? Math.Clamp((distance - _startDistance) / (_fullDistance - _startDistance), 0f, 1f)
            : distance > _startDistance ? 1f : 0f;

        var coverage = ent.Comp.Coverage;
        var effectiveAccuracy = GetEffectiveAccuracy(accuracy, toCover.Length());
        var accuracyPenalty = MathF.Max(0, _accuracyFactor * (1 - effectiveAccuracy / 100));
        var hitChance = MathF.Min(coverage, coverage * ramp + accuracyPenalty * ramp);
        var random = new Xoshiro128P(accuracy.GunSeed, (long) accuracy.Tick << 32 | (uint) GetNetEntity(ent).Id);
        if (random.NextFloat(0f, 100f) >= hitChance)
            args.GuaranteedMiss = true;
    }

    private bool IsShotOverCover(EntityUid cover, MapCoordinates coverPos, Vector2 direction)
    {
        if (direction == Vector2.Zero)
            return false;

        var behind = coverPos.Offset(-direction);
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator<StoriesProjectileCoverComponent>(behind);
        while (anchored.MoveNext(out var uid))
        {
            if (uid != cover && TryComp(uid, out StoriesProjectileCoverComponent? other) && !other.Directional)
                return true;
        }

        return false;
    }

    private static float GetEffectiveAccuracy(RMCProjectileAccuracyComponent projectile, float distance)
    {
        var accuracy = projectile.Accuracy;
        foreach (var threshold in projectile.Thresholds)
        {
            var pastRange = distance - threshold.Range;
            if (threshold.Buildup)
            {
                if (pastRange < 0)
                    accuracy += threshold.Falloff * pastRange;

                continue;
            }

            if (pastRange > 0)
                accuracy -= threshold.Falloff * pastRange;
        }

        if (accuracy < projectile.MinAccuracy)
            accuracy = projectile.MinAccuracy;

        return accuracy.Float();
    }
}
