using System.Numerics;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Stories.ZLevels.Combat;

public sealed class ZLevelCombatSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, ProjectileSetShooterEvent>(OnSetShooter);
        SubscribeLocalEvent<ProjectileComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ProjectileComponent, ProjectileShotEvent>(OnProjectileShot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZLevelProjectileTrajectoryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var traj, out var xform))
        {
            if (TerminatingOrDeleted(uid) || xform.MapUid is not { } currentMapUid)
                continue;

            var currentPos = _transform.GetWorldPosition(xform);
            var distTraveled = (currentPos - traj.StartPos).Length();

            if (traj.ZOffset < 0)
            {
                if (distTraveled >= 0.2f && _zLevels.IsTileTransparentOrEmpty(currentMapUid, currentPos))
                {
                    if (_zLevels.TryMoveDown(uid))
                    {
                        traj.ZOffset++;
                        if (traj.ZOffset == 0)
                        {
                            RemComp<ZLevelProjectileTrajectoryComponent>(uid);
                        }
                    }
                }
            }
            else if (traj.ZOffset > 0)
            {
                if (distTraveled >= 0.2f && !_zLevels.HasOpaqueAbove(currentPos, currentMapUid))
                {
                    if (_zLevels.TryMoveUp(uid))
                    {
                        traj.ZOffset--;
                        if (traj.ZOffset == 0)
                        {
                            RemComp<ZLevelProjectileTrajectoryComponent>(uid);
                        }
                    }
                }
            }
        }
    }

    private void OnSetShooter(Entity<ProjectileComponent> proj, ref ProjectileSetShooterEvent args)
    {
        ProcessShotProjectile(proj.Owner, proj.Comp, args.Shooter);
    }

    private void OnMapInit(Entity<ProjectileComponent> proj, ref MapInitEvent args)
    {
        if (proj.Comp.Shooter is { } shooter)
        {
            ProcessShotProjectile(proj.Owner, proj.Comp, shooter);
        }
    }

    private void OnProjectileShot(Entity<ProjectileComponent> proj, ref ProjectileShotEvent args)
    {
        var shooterUid = args.Shooter ?? proj.Comp.Shooter;
        ProcessShotProjectile(proj.Owner, proj.Comp, shooterUid);
    }

    private void ProcessShotProjectile(EntityUid proj, ProjectileComponent projComp, EntityUid? shooterUid)
    {
        if (HasComp<ZLevelProjectileTrajectoryComponent>(proj))
            return;

        var projXform = Transform(proj);
        if (projXform.MapUid is not { } projMapUid)
            return;

        if (shooterUid is not { } shooter || !Exists(shooter))
            return;

        var shooterXform = Transform(shooter);
        if (shooterXform.MapUid is not { } shooterMapUid)
            return;

        if (projMapUid != shooterMapUid)
            return;

        var shooterWorldPos = _transform.GetWorldPosition(shooterXform);
        var dir = _transform.GetWorldRotation(projXform).ToWorldVec();
        var targetWorldPos = shooterWorldPos + dir * 15f;

        var targetMapUid = (EntityUid?) null;
        var zOffset = 0;
        var lookUp = TryComp<ZLevelViewerComponent>(shooter, out var viewer) && viewer.LookUp;

        if (TryComp<TargetedProjectileComponent>(proj, out var targeted) &&
            TryGetEntity(targeted.Target, out var target) && target is { } targetUid && Exists(targetUid))
        {
            targetWorldPos = _transform.GetWorldPosition(targetUid);
            var tMapUid = Transform(targetUid).MapUid;
            for (var offset = -2; offset <= 2; offset++)
            {
                if (offset == 0) continue;
                if (_zLevels.TryMapOffset(shooterMapUid, offset, out var targetMap) && tMapUid == targetMap.Owner)
                {
                    targetMapUid = targetMap.Owner;
                    zOffset = offset;
                    break;
                }
            }
        }

        if (zOffset == 0 && lookUp)
        {
            for (var offset = 1; offset <= 2; offset++)
            {
                if (_zLevels.TryMapOffset(shooterMapUid, offset, out var mapAbove))
                {
                    zOffset = offset;
                    targetMapUid = mapAbove.Owner;
                    break;
                }
            }
        }

        if (zOffset == 0 && !lookUp)
        {
            if (TryComp<ZPhysicsComponent>(shooter, out var shooterZPhys) && shooterZPhys.LocalPosition > 0.2f)
            {
                if (_zLevels.TryMapOffset(shooterMapUid, 1, out var mapAbove))
                {
                    zOffset = 1;
                    targetMapUid = mapAbove.Owner;
                }
            }
        }

        if (zOffset == 0 && !lookUp)
        {
            if (_zLevels.TryMapOffset(shooterMapUid, -1, out var mapBelow))
            {
                var hasLedgeAhead = false;
                for (var step = 1; step <= 3; step++)
                {
                    var checkPos = shooterWorldPos + dir * step;
                    if (_zLevels.IsTileTransparentOrEmpty(shooterMapUid, checkPos))
                    {
                        hasLedgeAhead = true;
                        break;
                    }
                }

                if (hasLedgeAhead || _zLevels.IsTileTransparentOrEmpty(shooterMapUid, targetWorldPos))
                {
                    zOffset = -1;
                    targetMapUid = mapBelow.Owner;
                }
            }
        }

        if (zOffset != 0 && targetMapUid != null)
        {
            var totalDist = (targetWorldPos - shooterWorldPos).Length();
            if (totalDist < 0.5f)
                totalDist = 0.5f;

            var traj = AddComp<ZLevelProjectileTrajectoryComponent>(proj);
            traj.StartPos = shooterWorldPos;
            traj.TargetPos = targetWorldPos;
            traj.TotalDistance = totalDist;
            traj.TargetMapUid = targetMapUid.Value;
            traj.ZOffset = zOffset;
            traj.HasTransferred = false;
        }
    }
}
