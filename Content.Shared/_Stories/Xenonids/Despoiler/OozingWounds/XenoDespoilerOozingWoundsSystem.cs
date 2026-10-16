using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerOozingWoundsSystem : EntitySystem
{
    private static readonly int[] StackDrainCap = { int.MaxValue, 2, 1 };

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly XenoDespoilerCatalyzeFlagSystem _catalyze = default!;
    [Dependency] private readonly XenoDespoilerHypertensionSystem _hypertension = default!;

    private EntityQuery<XenoDespoilerAcidSprayComponent> _sprayQuery;
    private EntityQuery<XenoDespoilerLingeringAcidComponent> _lingeringQuery;

    public override void Initialize()
    {
        _sprayQuery = GetEntityQuery<XenoDespoilerAcidSprayComponent>();
        _lingeringQuery = GetEntityQuery<XenoDespoilerLingeringAcidComponent>();

        SubscribeLocalEvent<XenoDespoilerComponent, XenoDespoilerOozingWoundsActionEvent>(OnUse);
    }

    private void OnUse(EntityUid uid, XenoDespoilerComponent comp, XenoDespoilerOozingWoundsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<XenoDespoilerOozingWoundsActionComponent>(args.Action, out var action))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        if (action.CastSound is { } sound)
            _audio.PlayPredicted(sound, uid, uid);

        if (_net.IsClient)
            return;

        var severity = HealthSeverityTier(uid, action);

        if (TryComp<XenoDespoilerHypertensionComponent>(uid, out var hypertension))
        {
            var tier = Math.Clamp(severity, 0, StackDrainCap.Length - 1);
            var drain = Math.Min(hypertension.Stacks, StackDrainCap[tier]);
            _hypertension.ReduceStacks(uid, hypertension, drain);
        }

        var empowered = _catalyze.TakeEmpowerment(uid, comp);

        var radius = action.BaseRadius + severity;
        var origin = Transform(uid).Coordinates.SnapToGrid(EntityManager);
        var sprayProto = empowered ? action.AcidSprayEmpoweredProto : action.AcidSprayProto;
        var now = _timing.CurTime;

        var pending = EnsureComp<XenoDespoilerOozingWoundsPendingComponent>(uid);

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var euclid = MathF.Sqrt(dx * dx + dy * dy);
                if (euclid > radius + 0.001f)
                    continue;

                var cheby = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var tile = origin.Offset(new Vector2(dx, dy)).SnapToGrid(EntityManager);

                if (!_interaction.InRangeUnobstructed(uid, tile, radius + 1f))
                    continue;

                var spawnDelay = action.DistanceDelayPerTile * cheby;

                var telegraph = Spawn(action.TelegraphProto, tile);
                _hive.SetSameHive(uid, telegraph);

                if (spawnDelay > TimeSpan.Zero)
                    EnsureComp<TimedDespawnComponent>(telegraph).Lifetime = (float)spawnDelay.TotalSeconds;

                pending.Pending.Add(new XenoDespoilerOozingWoundsPendingTile
                {
                    SpawnAt = now + spawnDelay,
                    Tile = tile,
                    SprayProto = sprayProto,
                    PuddleProto = action.LingeringAcidProto,
                    PuddleChance = action.LingeringAcidChance,
                });
            }
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoDespoilerOozingWoundsPendingComponent>();
        while (query.MoveNext(out var caster, out var pending))
        {
            for (var i = pending.Pending.Count - 1; i >= 0; i--)
            {
                var entry = pending.Pending[i];
                if (now < entry.SpawnAt)
                    continue;

                pending.Pending.RemoveAt(i);

                if (TerminatingOrDeleted(caster))
                    continue;

                var spray = Spawn(entry.SprayProto, entry.Tile);
                _hive.SetSameHive(caster, spray);

                if (_sprayQuery.TryComp(spray, out var sprayComp))
                {
                    sprayComp.Caster = caster;
                    Dirty(spray, sprayComp);
                }

                if (_random.Prob(entry.PuddleChance))
                {
                    var puddle = Spawn(entry.PuddleProto, entry.Tile);
                    _hive.SetSameHive(caster, puddle);
                    if (_lingeringQuery.TryComp(puddle, out var puddleComp))
                    {
                        puddleComp.Caster = caster;
                        Dirty(puddle, puddleComp);
                    }
                }
            }

            if (pending.Pending.Count == 0)
                RemCompDeferred<XenoDespoilerOozingWoundsPendingComponent>(caster);
        }
    }

    private int HealthSeverityTier(EntityUid uid, XenoDespoilerOozingWoundsActionComponent action)
    {
        if (!TryComp<DamageableComponent>(uid, out var dmg))
            return 0;

        if (!_mobThresholds.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold) ||
            deadThreshold <= 0)
        {
            return 0;
        }

        var hpFraction = 1f - Math.Clamp((float)(dmg.TotalDamage / deadThreshold.Value), 0f, 1f);

        if (hpFraction <= action.SeverityHpThreshold2)
            return 2;

        return hpFraction <= action.SeverityHpThreshold1 ? 1 : 0;
    }
}
