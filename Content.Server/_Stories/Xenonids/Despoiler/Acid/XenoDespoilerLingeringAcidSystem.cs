using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.Despoiler;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerLingeringAcidSystem : EntitySystem
{
    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";
    private static readonly TimeSpan BarricadeDamageInterval = TimeSpan.FromSeconds(1);

    // One extinguisher pull fires several vapor puffs at once - collapse hits this close together into a single spray.
    private static readonly TimeSpan SpraySplitWindow = TimeSpan.FromSeconds(0.5);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<XenoComponent> _xenoQuery;
    private EntityQuery<PullableComponent> _pullableQuery;

    public override void Initialize()
    {
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _xenoQuery = GetEntityQuery<XenoComponent>();
        _pullableQuery = GetEntityQuery<PullableComponent>();

        SubscribeLocalEvent<XenoDespoilerLingeringAcidComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<XenoDespoilerLingeringAcidComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<XenoDespoilerLingeringAcidComponent, VaporHitEvent>(OnVaporHit);
    }

    private void OnMapInit(EntityUid uid, XenoDespoilerLingeringAcidComponent comp, MapInitEvent args)
    {
        using var anchored = _rmcMap.GetAnchoredEntitiesEnumerator<XenoDespoilerLingeringAcidComponent>(Transform(uid).Coordinates);
        while (anchored.MoveNext(out var existing))
        {
            if (existing != uid)
                QueueDel(existing);
        }

        comp.NextBarricadeDamageAt = _timing.CurTime + BarricadeDamageInterval;

        var min = (float)comp.MinLifetime.TotalSeconds;
        var max = (float)comp.MaxLifetime.TotalSeconds;
        var despawn = EnsureComp<TimedDespawnComponent>(uid);
        despawn.Lifetime = _random.NextFloat(min, max);
    }

    private void OnCollide(EntityUid uid, XenoDespoilerLingeringAcidComponent comp, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;
        if (!_mobStateQuery.HasComp(target) || _xenoQuery.HasComp(target))
            return;

        if (_pullableQuery.TryComp(target, out var pull) && pull.BeingPulled)
            return;

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Heat"] = FixedPoint2.New(comp.CrossBurnDamage);
        _damageable.TryChangeDamage(target, dmg, ignoreResistances: false, origin: comp.Caster);
        _slow.TrySlowdown(target, comp.CrossSlow);
    }

    private void OnVaporHit(Entity<XenoDespoilerLingeringAcidComponent> ent, ref VaporHitEvent args)
    {
        var water = false;
        foreach (var container in args.Solution.Comp.Containers)
        {
            if (!_solutionContainer.TryGetSolution(args.Solution.Owner, container, out _, out var solution))
                continue;

            if (solution.ContainsPrototype(WaterReagent))
            {
                water = true;
                break;
            }
        }

        if (!water)
            return;

        var now = _timing.CurTime;
        if (now - ent.Comp.LastSprayAt < SpraySplitWindow)
            return;

        ent.Comp.LastSprayAt = now;
        ent.Comp.SpraysTaken++;
        if (ent.Comp.SpraysTaken >= ent.Comp.SpraysToExtinguish)
            QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoDespoilerLingeringAcidComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextBarricadeDamageAt)
                continue;

            comp.NextBarricadeDamageAt = now + BarricadeDamageInterval;

            if (!_rmcMap.HasAnchoredEntityEnumerator<BarricadeComponent>(Transform(uid).Coordinates, out var barricade))
                continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Heat"] = FixedPoint2.New(comp.BarricadeDamagePerSecond);
            _damageable.TryChangeDamage(barricade.Owner, dmg, ignoreResistances: false, origin: comp.Caster);
        }
    }
}
