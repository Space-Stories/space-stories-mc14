using System.Linq;
using System.Numerics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Evasion;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Random;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Whitelist;
using Robust.Shared.Map; // Stories-ZLevels
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Projectiles;

public sealed class RMCProjectileSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly SharedProjectileSystem _sharedProjectile = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!; // Stories-ZLevels

    public override void Initialize()
    {
        SubscribeLocalEvent<DeleteOnCollideComponent, StartCollideEvent>(OnDeleteOnCollideStartCollide);
        SubscribeLocalEvent<ModifyTargetOnHitComponent, ProjectileHitEvent>(OnModifyTargetOnHit);
        SubscribeLocalEvent<ProjectileMaxRangeComponent, MapInitEvent>(OnProjectileMaxRangeMapInit);
        SubscribeLocalEvent<ProjectileMaxRangeComponent, PreventCollideEvent>(OnProjectileMaxRangePreventCollide);

        SubscribeLocalEvent<RMCProjectileDamageFalloffComponent, MapInitEvent>(OnFalloffProjectileMapInit);
        SubscribeLocalEvent<RMCProjectileDamageFalloffComponent, ProjectileHitEvent>(OnFalloffProjectileHit);

        SubscribeLocalEvent<RMCProjectileAccuracyComponent, MapInitEvent>(OnProjectileAccuracyMapInit);
        SubscribeLocalEvent<RMCProjectileAccuracyComponent, PreventCollideEvent>(OnProjectileAccuracyPreventCollide);

        SubscribeLocalEvent<SpawnOnTerminateComponent, MapInitEvent>(OnSpawnOnTerminatingMapInit);
        SubscribeLocalEvent<SpawnOnTerminateComponent, EntityTerminatingEvent>(OnSpawnOnTerminatingTerminate);
        SubscribeLocalEvent<SpawnOnTerminateComponent, ProjectileHitEvent>(OnSpawnOnTerminateProjectileHit);

        SubscribeLocalEvent<PreventCollideWithDeadComponent, PreventCollideEvent>(OnPreventCollideWithDead);
    }

    private void OnDeleteOnCollideStartCollide(Entity<DeleteOnCollideComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsServer)
            QueueDel(ent);
    }

    private void OnModifyTargetOnHit(Entity<ModifyTargetOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, args.Target))
            return;
        if (ent.Comp.Add is { } add)
            EntityManager.AddComponents(args.Target, add);
    }

    private void OnProjectileMaxRangeMapInit(Entity<ProjectileMaxRangeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Origin = _transform.GetMoverCoordinates(ent);
        Dirty(ent);
    }

    private void OnFalloffProjectileMapInit(Entity<RMCProjectileDamageFalloffComponent> projectile, ref MapInitEvent args)
    {
        projectile.Comp.ShotFrom = _transform.GetMoverCoordinates(projectile.Owner);
        Dirty(projectile);
    }

    // Stories-ZLevels-Start
    private bool TryGetRangeDistance(EntityCoordinates a, EntityCoordinates b, out float distance)
    {
        if (a.TryDistance(EntityManager, _transform, b, out distance))
            return true;

        var aMap = _transform.GetMapId(a);
        var bMap = _transform.GetMapId(b);
        if (aMap != bMap && aMap != MapId.Nullspace && bMap != MapId.Nullspace && _zLevels.AreInSameNetwork(aMap, bMap))
        {
            var aWorld = _transform.ToMapCoordinates(a).Position;
            var bWorld = _transform.ToMapCoordinates(b).Position;
            distance = Vector2.Distance(aWorld, bWorld);
            return true;
        }

        distance = 0f;
        return false;
    }

    private bool TryGetRangeDelta(EntityCoordinates a, EntityCoordinates b, out Vector2 delta)
    {
        if (a.TryDelta(EntityManager, _transform, b, out delta))
            return true;

        var aMap = _transform.GetMapId(a);
        var bMap = _transform.GetMapId(b);
        if (aMap != bMap && aMap != MapId.Nullspace && bMap != MapId.Nullspace && _zLevels.AreInSameNetwork(aMap, bMap))
        {
            var aWorld = _transform.ToMapCoordinates(a).Position;
            var bWorld = _transform.ToMapCoordinates(b).Position;
            delta = aWorld - bWorld;
            return true;
        }

        delta = Vector2.Zero;
        return false;
    }
    // Stories-ZLevels-End

    private void OnFalloffProjectileHit(Entity<RMCProjectileDamageFalloffComponent> projectile, ref ProjectileHitEvent args)
    {
        if (projectile.Comp.ShotFrom == null || projectile.Comp.MinRemainingDamageMult < 0)
            return;

        var targetCoords = _transform.GetMoverCoordinates(args.Target);
        if (!TryGetRangeDistance(targetCoords, projectile.Comp.ShotFrom.Value, out var distance))
            distance = (_transform.ToMapCoordinates(targetCoords).Position - _transform.ToMapCoordinates(projectile.Comp.ShotFrom.Value).Position).Length();
        var minDamage = args.Damage.GetTotal() * projectile.Comp.MinRemainingDamageMult;
        foreach (var threshold in projectile.Comp.Thresholds)
        {
            var pastEffectiveRange = distance - threshold.Range;

            if (pastEffectiveRange <= 0)
                continue;

            var totalDamage = args.Damage.GetTotal();
            if (totalDamage <= minDamage)
                break;

            var extraMult = threshold.IgnoreModifiers ? 1 : projectile.Comp.WeaponMult;
            var minMult = FixedPoint2.Min(minDamage / totalDamage, 1);

            args.Damage *= FixedPoint2.Clamp((totalDamage - pastEffectiveRange * threshold.Falloff * extraMult) / totalDamage, minMult, 1);
        }
    }

    public void SetProjectileFalloffWeaponMult(Entity<RMCProjectileDamageFalloffComponent> projectile, FixedPoint2 mult, float range)
    {
        var count = 0;
        while (projectile.Comp.Thresholds.Count > count)
        {
            var threshold = projectile.Comp.Thresholds[count];
            projectile.Comp.Thresholds[count] = threshold with { Range = threshold.Range + range };
            count++;
        }

        projectile.Comp.WeaponMult = mult;
        Dirty(projectile);
    }

    private void OnProjectileAccuracyMapInit(Entity<RMCProjectileAccuracyComponent> projectile, ref MapInitEvent args)
    {
        projectile.Comp.ShotFrom = _transform.GetMoverCoordinates(projectile.Owner);
        projectile.Comp.Tick = _timing.CurTick.Value;

        Dirty(projectile);
    }

    public bool ShouldHitTarget(EntityUid projectileUid, RMCProjectileAccuracyComponent? accuracyComp, EntityUid target, ProjectileComponent? projectileComponent = null)
    {
        var netOther = GetNetEntity(target);
        if (accuracyComp != null && accuracyComp.Dodged.Contains(netOther))
            return false;

        var ev = new RMCBeforeProjectileAccuracyEvent(projectileUid);
        RaiseLocalEvent(target, ref ev);

        if (ev.GuaranteedMiss)
        {
            if (accuracyComp != null)
            {
                accuracyComp.Dodged.Add(netOther);
                Dirty(projectileUid, accuracyComp);
            }
            return false;
        }

        if (accuracyComp == null || accuracyComp.ForceHit || accuracyComp.ShotFrom == null)
            return true;

        if (!Resolve(projectileUid, ref projectileComponent, false))
            return true;

        if (!TryComp(target, out EvasionComponent? evasionComponent))
            return true;

        var accuracy = accuracyComp.Accuracy;
        var targetCoords = _transform.GetMoverCoordinates(target);
        var distance = (targetCoords.Position - accuracyComp.ShotFrom.Value.Position).Length();

        foreach (var threshold in accuracyComp.Thresholds)
        {
            var pastRange = distance - threshold.Range;

            if (threshold.Buildup)
            {
                if (pastRange >= 0)
                    continue;

                accuracy += threshold.Falloff * pastRange;
                continue;
            }

            if (pastRange <= 0)
                continue;

            accuracy -= threshold.Falloff * pastRange;
        }

        if (!_examine.InRangeUnOccluded(_transform.ToMapCoordinates(accuracyComp.ShotFrom.Value), _transform.ToMapCoordinates(targetCoords), distance, null))
            accuracy += (int)AccuracyModifiers.TargetOccluded;

        if (!accuracyComp.IgnoreFriendlyEvasion && IsProjectileTargetFriendly(projectileUid, target))
            accuracy -= evasionComponent.ModifiedEvasionFriendly;

        accuracy -= evasionComponent.ModifiedEvasion;

        accuracy = accuracy > accuracyComp.MinAccuracy ? accuracy : accuracyComp.MinAccuracy;

        var random = new Xoshiro128P(accuracyComp.GunSeed, (long)accuracyComp.Tick << 32 | netOther.Id).NextFloat(0f, 100f);

        if (accuracy >= random)
            return true;

        accuracyComp.Dodged.Add(netOther);
        Dirty(projectileUid, accuracyComp);
        return false;
    }

    private void OnProjectileAccuracyPreventCollide(Entity<RMCProjectileAccuracyComponent> projectile, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ShouldHitTarget(projectile.Owner, projectile.Comp, args.OtherEntity))
            args.Cancelled = true;
    }

    private bool IsProjectileTargetFriendly(EntityUid projectile, EntityUid target)
    {
        if (!TryComp(projectile, out ProjectileComponent? projectileComp) || projectileComp.Shooter == null)
            return false;

        return _npcFaction.IsEntityFriendly(projectileComp.Shooter.Value, target);
    }

    private void OnSpawnOnTerminatingMapInit(Entity<SpawnOnTerminateComponent> ent, ref MapInitEvent args)
    {
        // Stories-ZLevels-Start
        var xform = Transform(ent);
        if (xform.MapUid is { } mapUid)
        {
            var worldPos = _transform.GetWorldPosition(xform);
            ent.Comp.Origin = new EntityCoordinates(mapUid, worldPos);
            Dirty(ent);
        }
        // Stories-ZLevels-End
    }

    private void OnSpawnOnTerminatingTerminate(Entity<SpawnOnTerminateComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(ent, out TransformComponent? transform))
            return;

        if (TerminatingOrDeleted(transform.ParentUid))
            return;

        var coordinates = transform.Coordinates;
        if (ent.Comp.Origin is { } origin &&
            TryGetRangeDelta(coordinates, origin, out var delta)) // Stories-ProjectileDirection
        {
            var deltaLength = delta.Length();

            if (deltaLength > 0f)
            {
                var direction = delta / deltaLength;

                if (TryComp(ent, out ProjectileMaxRangeComponent? projectileMaxRange) &&
                    deltaLength > projectileMaxRange.Max)
                {
                    deltaLength = projectileMaxRange.Max;
                    delta = direction * deltaLength;
                    var curMap = _transform.GetMapId(coordinates);
                    var origMap = _transform.GetMapId(origin);
                    if (curMap != origMap)
                    {
                        var originWorld = _transform.ToMapCoordinates(origin).Position;
                        var clampedWorld = originWorld + delta;
                        coordinates = new EntityCoordinates(transform.ParentUid, clampedWorld - _transform.GetWorldPosition(transform.ParentUid));
                    }
                    else
                    {
                        coordinates = origin.Offset(delta);
                    }
                }

                if (ent.Comp.AdjustSpawn && ent.Comp.SpawnOffset != 0f)
                    coordinates = coordinates.Offset(direction * ent.Comp.SpawnOffset);
            }
        }

        var spawn = SpawnAtPosition(ent.Comp.Spawn, coordinates);
        _hive.SetSameHive(ent.Owner, spawn);
        if (HasComp<TileFireComponent>(spawn))
        {
            var fires = _rmcMap.GetAnchoredEntitiesEnumerator<TileFireComponent>(coordinates);
            while (fires.MoveNext(out var fire))
            {
                if (fire != spawn)
                    QueueDel(fire);
            }
        }

        if (ent.Comp.Popup is { } popup)
            _popup.PopupCoordinates(Loc.GetString(popup), coordinates, ent.Comp.PopupType ?? PopupType.Small);
    }

    public void SetSpawn(Entity<SpawnOnTerminateComponent> ent, EntProtoId spawn)
    {
        ent.Comp.Spawn = spawn;
        Dirty(ent);
    }

    private void OnSpawnOnTerminateProjectileHit(Entity<SpawnOnTerminateComponent> ent, ref ProjectileHitEvent args)
    {
        ent.Comp.AdjustSpawn = true;
        Dirty(ent);
    }

    public void SetSpawnOffset(Entity<SpawnOnTerminateComponent> ent, float offset)
    {
        ent.Comp.SpawnOffset = offset;
        Dirty(ent);
    }

    private void OnPreventCollideWithDead(Entity<PreventCollideWithDeadComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (_mobState.IsDead(args.OtherEntity))
            args.Cancelled = true;
    }

    public void SetMaxRange(EntityUid projectile, float max)
    {
        var maxRange = EnsureComp<ProjectileMaxRangeComponent>(projectile);

        maxRange.Max = max;
        Dirty(projectile, maxRange);
    }

    public void ModifyDamage(EntityUid projectile, int multiplier)
    {
        if (!TryComp<ProjectileComponent>(projectile, out var comp))
            return;

        comp.Damage *= multiplier;
        Dirty(projectile, comp);
    }

    private void StopProjectile(Entity<ProjectileMaxRangeComponent> ent)
    {
        if (ent.Comp.Delete)
        {
            if (_net.IsServer || IsClientSide(ent))
                QueueDel(ent);
        }
        else
        {
            _physics.SetLinearVelocity(ent, Vector2.Zero);
            RemCompDeferred<ProjectileMaxRangeComponent>(ent);
        }
    }

    private void OnProjectileMaxRangePreventCollide(Entity<ProjectileMaxRangeComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Origin is not { } origin)
            return;

        if (!TryGetRangeDistance(origin, _transform.GetMoverCoordinates(ent), out var distance))
            return;

        if (distance < ent.Comp.Max)
            return;

        args.Cancelled = true;
        StopProjectile(ent);
    }

    public void SetProjectileAccuracy(EntityUid projectile, float accuracy)
    {
        var accuracyComponent = EnsureComp<RMCProjectileAccuracyComponent>(projectile);

        accuracyComponent.Accuracy = accuracy;
        Dirty(projectile, accuracyComponent);
    }

    public override void Update(float frameTime)
    {
        var maxQuery = EntityQueryEnumerator<ProjectileMaxRangeComponent>();
        while (maxQuery.MoveNext(out var uid, out var comp))
        {
            var coordinates = _transform.GetMoverCoordinates(uid);
            if (comp.Origin is not { } origin ||
                !TryGetRangeDistance(coordinates, origin, out var distance))
            {
                StopProjectile((uid, comp));
                continue;
            }

            if (distance < comp.Max && Math.Abs(distance - comp.Max) > 0.1f)
                continue;

            StopProjectile((uid, comp));
        }

        UpdateProjectileCcd(); // Stories-Projectiles
    }

    // Stories-Projectiles-Start
    private void UpdateProjectileCcd()
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var projectile, out var physics, out var xform))
        {
            if (projectile.ProjectileSpent || !physics.CanCollide || physics.BodyStatus != BodyStatus.InAir)
            {
                projectile.LastCcdPosition = null;
                continue;
            }

            var curPos = _transform.GetWorldPosition(xform);
            if (projectile.LastCcdPosition == null)
            {
                projectile.LastCcdPosition = curPos;
                continue;
            }

            var prevPos = projectile.LastCcdPosition.Value;
            projectile.LastCcdPosition = curPos;

            var displacement = curPos - prevPos;
            var distSq = displacement.LengthSquared();
            // If the projectile moved less than 0.25 tiles, Box2D discrete overlap catches it.
            if (distSq < 0.0625f)
                continue;

            var dist = MathF.Sqrt(distSq);
            var dir = displacement / dist;

            if (_fixtures.GetFixtureOrNull(uid, SharedProjectileSystem.ProjectileFixture) is not { } fixture)
                continue;

            var mapId = xform.MapID;
            var ray = new CollisionRay(prevPos, dir, fixture.CollisionMask);
            var results = _physics.IntersectRayWithPredicate(mapId, ray, dist, target =>
            {
                if (target == uid)
                    return true;

                if (projectile.IgnoreShooter && (target == projectile.Shooter || target == projectile.Weapon))
                    return true;

                return false;
            }, returnOnFirstHit: false);

            var sortedResults = results.OrderBy(r => r.Distance);
            foreach (var hit in sortedResults)
            {
                if (projectile.ProjectileSpent || TerminatingOrDeleted(uid))
                    break;

                var target = hit.HitEntity;
                if (TerminatingOrDeleted(target))
                    continue;

                if (!TryComp<PhysicsComponent>(target, out var targetPhysics))
                    continue;

                if (!TryComp<FixturesComponent>(target, out var targetFixtures) || targetFixtures.Fixtures.Count == 0)
                    continue;

                var targetFixture = targetFixtures.Fixtures.Values.FirstOrDefault(f => (f.CollisionLayer & fixture.CollisionMask) != 0)
                    ?? targetFixtures.Fixtures.Values.First();

                // Respect PreventCollideEvent on both projectile and target (RequireProjectileTargetSystem,
                // GunPredictionSystem, PreventCollideWithDeadComponent, VehicleRideSurface, etc.)
                var projectileEv = new PreventCollideEvent(uid, target, physics, targetPhysics, fixture, targetFixture);
                RaiseLocalEvent(uid, ref projectileEv);
                if (projectileEv.Cancelled)
                    continue;

                var targetEv = new PreventCollideEvent(target, uid, targetPhysics, physics, targetFixture, fixture);
                RaiseLocalEvent(target, ref targetEv);
                if (targetEv.Cancelled)
                    continue;

                // Accuracy and evasion checks
                TryComp<RMCProjectileAccuracyComponent>(uid, out var accuracyComp);
                if (!ShouldHitTarget(uid, accuracyComp, target, projectile))
                    continue;

                // Bullet hit the target along its swept path!
                _transform.SetWorldPosition(uid, hit.HitPos);
                _sharedProjectile.ProjectileCollide((uid, projectile, physics), target);
                break;
            }
        }
    }
}

// Stories-Projectiles-Start
[ByRefEvent]
public readonly record struct ProjectileShotEvent(EntityUid? Shooter, bool Predicted = true);
// Stories-Projectiles-End
