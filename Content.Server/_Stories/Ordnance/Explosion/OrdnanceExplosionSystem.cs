using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._Stories.Ordnance;
using Content.Shared._Stories.Ordnance.Chemistry.ReactionEffects;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Sticky.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stories.Ordnance.Explosion;

public sealed class OrdnanceExplosionSystem : EntitySystem
{
    [Dependency] private readonly SharedOrdnanceCasingSystem _casing = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _map = default!;

    private readonly HashSet<EntityUid> _wallsCache = new();
    private readonly List<ReagentQuantity> _reagentCache = new();

    private static readonly ProtoId<ExplosionPrototype> RmcExplosion = "RMC";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SensitiveReactionExplosionEvent>(OnReactionExplosion);
        SubscribeLocalEvent<OrdnanceNoFuelDetonateComponent, Robust.Shared.Physics.Events.StartCollideEvent>(OnNoFuelCollide);
    }

    private void OnNoFuelCollide(Entity<OrdnanceNoFuelDetonateComponent> ent, ref Robust.Shared.Physics.Events.StartCollideEvent args)
    {
        if (TryComp<OrdnanceCasingComponent>(ent, out var casing))
        {
            var effectiveCasing = _casing.GetEffectiveCasing(ent, casing, out var effectiveUid);
            if (!_casing.HasValidTrigger(effectiveUid, effectiveCasing))
            {
                _popup.PopupEntity(Loc.GetString("stories-ordnance-fizzle", ("casing", Name(ent))), ent, PopupType.Small);
            }
            else
            {
                var evDetonate = new OrdnanceDetonateEvent(null);
                RaiseLocalEvent(ent, ref evDetonate);
            }

            RemComp<OrdnanceNoFuelDetonateComponent>(ent);
        }
    }

    private void OnReactionExplosion(ref SensitiveReactionExplosionEvent args)
    {
        var mapCoords = _transform.ToMapCoordinates(Transform(args.Target).Coordinates);
        _explosion.QueueExplosion(mapCoords,
            args.ExplosionType,
            args.MaxTotalIntensity,
            args.Slope,
            args.Intensity,
            null,
            canCreateVacuum: false);
    }

    public void PopulateContentsCache(EntityUid uid, OrdnanceCasingComponent casing, List<ReagentQuantity> cache)
    {
        if (casing.IsAssembly)
        {
            if (_itemSlots.TryGetSlot(uid, casing.BeakerSlot1Id, out var slot1) && slot1.Item != null &&
                _solutions.TryGetFitsInDispenser(slot1.Item.Value, out _, out var sol1))
                cache.AddRange(sol1.Contents);

            if (_itemSlots.TryGetSlot(uid, casing.BeakerSlot2Id, out var slot2) && slot2.Item != null &&
                _solutions.TryGetFitsInDispenser(slot2.Item.Value, out _, out var sol2))
                cache.AddRange(sol2.Contents);

            if (_itemSlots.TryGetSlot(uid, casing.WarheadSlotId, out var warheadSlot) && warheadSlot.Item != null)
            {
                if (TryComp<OrdnanceCasingComponent>(warheadSlot.Item.Value, out var warheadCasing))
                {
                    PopulateContentsCache(warheadSlot.Item.Value, warheadCasing, cache);
                }
            }
        }

        if (TryComp<SolutionContainerManagerComponent>(uid, out var solMan))
        {
            foreach (var (_, solution) in _solutions.EnumerateSolutions((uid, solMan)))
            {
                cache.AddRange(solution.Comp.Solution.Contents);
            }
        }
    }

    public OrdnanceCasingComponent GetEffectiveCasing(EntityUid uid, OrdnanceCasingComponent casing)
    {
        return _casing.GetEffectiveCasing(uid, casing);
    }

    public EngineExplosionParams GetEngineExplosionParams(ExplosionStats stats)
    {
        if (stats.Power <= 0 || stats.Falloff <= 0)
            return new EngineExplosionParams(0, 0, 0, 0);

        var maxIntensity = stats.Power / 5f;
        var slope = Math.Max(stats.Falloff / 5f, 0.1f);

        var radius = slope > 0 ? maxIntensity / slope : 0f;
        var calcRadius = Math.Max(0f, radius - 1f);
        var totalIntensity = Math.Max((float)(Math.PI / 3.0 * slope * Math.Pow(calcRadius, 3)), maxIntensity);

        return new EngineExplosionParams(totalIntensity, slope, maxIntensity, radius);
    }

    public void Detonate(Entity<OrdnanceCasingComponent> ent, EntityUid? user)
    {
        var uid = ent.Owner;
        var comp = ent.Comp;

        var coordinates = _transform.GetMoverCoordinates(uid);
        var effectiveCasing = _casing.GetEffectiveCasing(uid, comp, out var effectiveUid);

        if (!_casing.HasValidTrigger(effectiveUid, effectiveCasing))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("stories-ordnance-fizzle", ("casing", Name(uid))), uid, user.Value);

            RemComp<ActiveTimerTriggerComponent>(uid);
            return;
        }

        _reagentCache.Clear();
        PopulateContentsCache(uid, comp, _reagentCache);

        var stats = CalculateExplosionStats(_reagentCache, comp, effectiveCasing);

        if (stats.Power <= 0 && stats.FireRadius <= 0 && stats.Shards <= 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("stories-ordnance-fizzle", ("casing", Name(uid))), uid, user.Value);

            RemComp<ActiveTimerTriggerComponent>(uid);
            return;
        }

        ExecuteExplosion(coordinates, stats, effectiveCasing, uid, user);
        QueueDel(uid);
    }

    public void ExecuteExplosion(EntityCoordinates coords, ExplosionStats stats, OrdnanceCasingComponent comp, EntityUid sourceUid, EntityUid? user)
    {
        var engineParams = GetEngineExplosionParams(stats);

        if (engineParams.TotalIntensity > 0)
        {
            var mapCoordinates = _transform.ToMapCoordinates(coords);
            _explosion.QueueExplosion(mapCoordinates,
                RmcExplosion,
                engineParams.TotalIntensity,
                engineParams.Slope,
                engineParams.MaxIntensity,
                user,
                canCreateVacuum: false);
        }

        SpawnShrapnel(sourceUid, coords, comp, stats, user);

        var gridCoords = coords.SnapToGrid(EntityManager, _map);

        if (stats.FireRadius > 0 && stats.FireIntensity > 0)
        {
            if (comp.AllowStarShape && stats.FireIntensity > 30)
            {
                var rayRange = (int)Math.Round(stats.FireRadius * 1.5f);
                rayRange = Math.Min(rayRange, (int)comp.MaxFireRadius);
                _flammable.SpawnFireLines(stats.FireEntity, gridCoords, rayRange, rayRange, (int)stats.FireIntensity, (int)stats.FireDuration, stats.FireColor);
            }
            else
            {
                _flammable.SpawnFireDiamond(stats.FireEntity, gridCoords, (int)stats.FireRadius, (int)stats.FireIntensity, (int)stats.FireDuration, stats.FireColor);
            }
        }

        if (HasComp<StickyComponent>(sourceUid))
        {
            _wallsCache.Clear();
            _lookup.GetEntitiesInRange(coords, 1.0f, _wallsCache);
            foreach (var w in _wallsCache)
            {
                if (HasComp<RMCWallExplosionDeletableComponent>(w))
                    QueueDel(w);
            }

            if (TryComp<StickyComponent>(sourceUid, out var sticky))
            {
                if (sticky.StuckTo != null && HasComp<XenoTunnelComponent>(sticky.StuckTo.Value))
                {
                    QueueDel(sticky.StuckTo.Value);
                }
            }
        }

        var ev = new CMExplosiveTriggeredEvent();
        RaiseLocalEvent(sourceUid, ref ev);
    }

    public ExplosionStats CalculateExplosionStats(IEnumerable<ReagentQuantity> contents, OrdnanceCasingComponent? baseCasing = null, OrdnanceCasingComponent? effectiveCasing = null)
    {
        var casing = effectiveCasing ?? baseCasing;

        var baseFalloff = casing?.BaseFalloff ?? 75f;
        var minFalloff = casing?.MinFalloff ?? 25f;
        var maxPower = casing?.MaxExplosionPower ?? 175f;
        var maxShards = casing?.MaxShards ?? 16;
        var maxFireRadius = casing?.MaxFireRadius ?? 5f;
        var maxFireIntensity = casing?.MaxFireIntensity ?? 20f;
        var maxFireDuration = casing?.MaxFireDuration ?? 24f;

        var minFireIntensity = casing?.MinFireIntensity ?? 3f;
        var minFireDuration = casing?.MinFireDuration ?? 3f;
        var minFireRadius = casing?.MinFireRadius ?? 1f;

        var ironId = casing?.IronReagent ?? "RMCIron";

        var exPower = 0f;
        var exFalloff = baseFalloff;

        var fireIntensity = 0f;
        var fireDuration = 0f;
        var fireRadius = 0f;

        float r = 0, g = 0, b = 0;
        var colorWeightTotal = 0f;

        var shards = 0;
        var fireProto = "RMCTileFire";
        var firePenetrating = false;

        foreach (var reagentQuantity in contents)
        {
            var qty = reagentQuantity.Quantity.Float();
            if (qty <= 0) continue;

            if (!_reagents.TryIndex(reagentQuantity.Reagent.Prototype, out var proto))
                continue;

            if (proto.Explosive)
            {
                exPower += qty * proto.Power;
                exFalloff += qty * proto.FalloffModifier;
            }

            if (proto.FireSpread || proto.IntensityMod != 0 || proto.DurationMod != 0 || proto.RadiusMod != 0)
            {
                fireIntensity += qty * proto.IntensityMod;
                fireDuration += qty * proto.DurationMod;
                fireRadius += qty * proto.RadiusMod;
            }

            if (proto.BurnColor != null)
            {
                var weight = MathF.Max(qty, 1f) * proto.Burncolormod;
                r += proto.BurnColor.Value.R * weight;
                g += proto.BurnColor.Value.G * weight;
                b += proto.BurnColor.Value.B * weight;
                colorWeightTotal += weight;
            }

            if (proto.FireEntity != "RMCTileFire")
                fireProto = proto.FireEntity;

            firePenetrating |= proto.FirePenetrating;

            if (reagentQuantity.Reagent.Prototype == ironId)
                shards += (int)(qty * 0.25f);

            if (proto.Metabolisms != null)
            {
                foreach (var metabolism in proto.Metabolisms.Values)
                {
                    if (metabolism.Effects == null) continue;
                    foreach (var effect in metabolism.Effects)
                    {
                        if (effect is IExplosionModifierEffect modEffect)
                        {
                            var level = 1f;
                            if (effect is RMCChemicalEffect rmcEffect)
                                level = rmcEffect.Potency;

                            modEffect.ModifyExplosionStats(ref exPower, ref exFalloff, ref fireIntensity, ref fireDuration, ref fireRadius, qty, level);
                        }
                    }
                }
            }
        }

        if (exPower <= 0)
            shards = 0;

        exPower = Math.Min(exPower, maxPower);
        exFalloff = Math.Max(exFalloff, minFalloff);

        var hasDampener = (baseCasing?.BlastDampener == true) || (effectiveCasing?.BlastDampener == true);
        if (hasDampener)
            exFalloff *= 2f;

        shards = Math.Min(shards, maxShards);

        Color? finalColor = null;
        if (colorWeightTotal > 0)
            finalColor = new Color(r / colorWeightTotal, g / colorWeightTotal, b / colorWeightTotal, 1f);

        if (fireIntensity > 0)
        {
            fireRadius = Math.Clamp(fireRadius, minFireRadius, maxFireRadius);
            fireIntensity = Math.Clamp(fireIntensity, minFireIntensity, maxFireIntensity);
            fireDuration = Math.Clamp(fireDuration, minFireDuration, maxFireDuration);

            if (finalColor != null)
                fireProto = firePenetrating ? "STTileFireDynamicPenetrating" : "STTileFireDynamic";
        }
        else
        {
            fireRadius = 0;
            fireDuration = 0;
            fireIntensity = 0;
        }

        return new ExplosionStats
        {
            Power = exPower,
            Falloff = exFalloff,
            Shards = shards,
            FireIntensity = fireIntensity,
            FireDuration = fireDuration,
            FireRadius = fireRadius,
            FireEntity = fireProto,
            FirePenetrating = firePenetrating,
            FireColor = finalColor
        };
    }

    private void SpawnShrapnel(EntityUid sourceUid, EntityCoordinates coords, OrdnanceCasingComponent comp, ExplosionStats stats, EntityUid? user)
    {
        if (stats.Shards <= 0)
            return;

        var minAngle = Angle.Zero;
        var maxAngle = Angle.FromDegrees(360);

        if (comp.UseDirection)
        {
            var sourceAngle = _transform.GetWorldRotation(sourceUid);
            var halfAngle = Angle.FromDegrees(comp.ConeAngle / 2);
            minAngle = sourceAngle - halfAngle;
            maxAngle = sourceAngle + halfAngle;
        }

        for (var i = 0; i < stats.Shards; i++)
        {
            var shrapnel = Spawn(comp.ShrapnelProto, coords);
            var randomAngle = _random.NextAngle(minAngle, maxAngle);

            if (HasComp<ProjectileComponent>(shrapnel))
            {
                var direction = randomAngle.ToWorldVec();
                _gun.ShootProjectile(shrapnel, direction, Vector2.Zero, sourceUid, user);
            }
        }
    }
}
