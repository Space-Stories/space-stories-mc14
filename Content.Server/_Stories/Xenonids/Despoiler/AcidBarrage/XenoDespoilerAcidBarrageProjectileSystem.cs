using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.Despoiler;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;

namespace Content.Server._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerAcidBarrageProjectileSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedXenoDespoilerAcidSystem _acid = default!;

    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<XenoComponent> _xenoQuery;

    public override void Initialize()
    {
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _xenoQuery = GetEntityQuery<XenoComponent>();

        SubscribeLocalEvent<XenoDespoilerAcidBarrageProjectileComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(EntityUid uid, XenoDespoilerAcidBarrageProjectileComponent comp, ref ProjectileHitEvent args)
    {
        if (args.Handled || comp.Shooter is not { } shooter)
            return;

        if (_mobStateQuery.HasComp(args.Target) && !_xenoQuery.HasComp(args.Target))
            _acid.ApplyAcid(args.Target, shooter);

        foreach (var marine in _lookup.GetEntitiesInRange<MarineComponent>(_transform.GetMapCoordinates(args.Target), 1f))
        {
            if (marine.Owner != args.Target)
                _acid.ApplyAcid(marine, shooter);
        }
    }
}
