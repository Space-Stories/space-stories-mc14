using System.Collections.Generic;
using Content.Server._Stories.Ordnance.Explosion;
using Content.Server.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Explosion;
using Content.Shared._Stories.Ordnance;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.Components.OnTrigger;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Sticky.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Ordnance;

public sealed class OrdnanceCasingSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly FixtureSystem _fixture = default!;
    [Dependency] private readonly SharedOrdnanceCasingSystem _ordnanceCasing = default!;
    [Dependency] private readonly OrdnanceExplosionSystem _ordnanceExplosion = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    private static readonly ProtoId<TagPrototype> LaunchTubeTag = "RMCLaunchTube";

    private readonly List<ReagentQuantity> _reagentCache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrdnanceCasingComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<OrdnanceCasingComponent, OrdnancePulseEvent>(OnPulse);
        SubscribeLocalEvent<OrdnanceCasingComponent, OrdnanceDetonateEvent>(OnDetonateEvent);
        SubscribeLocalEvent<OrdnanceCasingComponent, OrdnanceCasingLockedEvent>(OnCasingLocked);

        SubscribeLocalEvent<OrdnanceCustomWarheadProjectileComponent, TriggerEvent>(OnCustomWarheadTrigger);
        SubscribeLocalEvent<OrdnanceCustomWarheadProjectileComponent, ProjectileHitEvent>(OnCustomWarheadHit);

        SubscribeLocalEvent<GunComponent, TakeAmmoEvent>(OnGunTakeAmmo);
        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnGunAmmoShot);
    }

    private void OnCasingLocked(Entity<OrdnanceCasingComponent> ent, ref OrdnanceCasingLockedEvent args)
    {
        var effCasing = _ordnanceCasing.GetEffectiveCasing(ent.Owner, ent.Comp, out var effUid);

        _reagentCache.Clear();
        _ordnanceExplosion.PopulateContentsCache(ent.Owner, ent.Comp, _reagentCache);

        var stats = _ordnanceExplosion.CalculateExplosionStats(_reagentCache, ent.Comp, effCasing);

        var valid = _ordnanceCasing.HasValidTrigger(effUid, effCasing) && (stats.Power > 0 || stats.FireRadius > 0 || stats.Shards > 0);

        if (!valid)
        {
            RemComp<OnUseTimerTriggerComponent>(ent.Owner);
            RemComp<CMVocalizeTriggerComponent>(ent.Owner);
        }
    }

    private void OnCustomWarheadHit(Entity<OrdnanceCustomWarheadProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (TryComp<OrdnanceCasingComponent>(ent.Comp.WarheadUid, out var warheadCasing))
        {
            if (_container.TryGetContainingContainer((ent.Comp.WarheadUid, null), out var container))
            {
                _container.Remove(ent.Comp.WarheadUid, container);
            }

            _transform.SetCoordinates(ent.Comp.WarheadUid, _transform.GetMoverCoordinates(ent));
            _ordnanceExplosion.Detonate((ent.Comp.WarheadUid, warheadCasing), args.Shooter);
        }
        args.Handled = true;
        QueueDel(ent);
    }

    private void OnCustomWarheadTrigger(Entity<OrdnanceCustomWarheadProjectileComponent> ent, ref TriggerEvent args)
    {
        if (TryComp<OrdnanceCasingComponent>(ent.Comp.WarheadUid, out var warheadCasing))
        {
            if (_container.TryGetContainingContainer((ent.Comp.WarheadUid, null), out var container))
            {
                _container.Remove(ent.Comp.WarheadUid, container);
            }

            _transform.SetCoordinates(ent.Comp.WarheadUid, _transform.GetMoverCoordinates(ent));
            _ordnanceExplosion.Detonate((ent.Comp.WarheadUid, warheadCasing), CompOrNull<ProjectileComponent>(ent)?.Shooter);
        }
        args.Handled = true;
        QueueDel(ent);
    }

    private void OnTrigger(Entity<OrdnanceCasingComponent> ent, ref TriggerEvent args)
    {
        var user = TryComp<ProjectileComponent>(ent, out var projectile) ? projectile.Shooter : args.User;
        _ordnanceExplosion.Detonate(ent, user);
        args.Handled = true;
    }

    private void OnPulse(Entity<OrdnanceCasingComponent> ent, ref OrdnancePulseEvent args)
    {
        if (ent.Comp.IsLocked)
        {
            if (ent.Comp.RequiredAssemblyMode == "Plastic")
            {
                if (!TryComp<StickyComponent>(ent, out var sticky) || sticky.StuckTo == null)
                    return;
            }

            if (ent.Comp.SignallerDelay > 0)
            {
                if (HasComp<ActiveTimerTriggerComponent>(ent))
                    return;

                SoundSpecifier? beepSound = null;
                if (TryComp<RMCExplosiveDeleteComponent>(ent, out var del))
                    beepSound = del.BeepSound;

                _trigger.HandleTimerTrigger(ent, args.User, ent.Comp.SignallerDelay, 0.5f, 0f, beepSound);
                _ordnanceCasing.UpdateAppearance(ent);
            }
            else
            {
                _ordnanceExplosion.Detonate(ent, args.User);
            }
        }
    }

    private void OnDetonateEvent(Entity<OrdnanceCasingComponent> ent, ref OrdnanceDetonateEvent args)
    {
        _ordnanceExplosion.Detonate(ent, args.User);
    }

    private void OnGunTakeAmmo(Entity<GunComponent> ent, ref TakeAmmoEvent args)
    {
        if (TryComp<OrdnanceCasingComponent>(ent.Owner, out var gunCasing) && _tag.HasTag(ent.Owner, LaunchTubeTag))
        {
            var effectiveCasing = _ordnanceCasing.GetEffectiveCasing(ent.Owner, gunCasing, out var effectiveUid);
            var hasTrigger = _ordnanceCasing.HasValidTrigger(effectiveUid, effectiveCasing);
            var hasFuel = _ordnanceCasing.HasFuel(ent.Owner, gunCasing);

            for (var i = args.Ammo.Count - 1; i >= 0; i--)
            {
                var ammo = args.Ammo[i];
                if (ammo.Entity == null) continue;
                var ammoUid = ammo.Entity.Value;

                if (!hasFuel)
                {
                    args.Ammo.RemoveAt(i);

                    if (hasTrigger)
                    {
                        DetonateFuelLessAmmo(ent.Owner, ent.Owner, args.User);
                    }
                    else
                    {
                        if (args.User.HasValue)
                            _popup.PopupEntity(Loc.GetString("stories-ordnance-fizzle", ("casing", Name(ent.Owner))), ent.Owner, args.User.Value, PopupType.Small);

                        _transform.SetCoordinates(ammoUid, _transform.GetMoverCoordinates(ent.Owner));
                    }
                }
                else
                {
                    _ordnanceCasing.TryConsumeFuel(ent.Owner, gunCasing);
                }
            }
        }
        else
        {
            for (var i = args.Ammo.Count - 1; i >= 0; i--)
            {
                var ammo = args.Ammo[i];
                if (ammo.Entity != null && TryComp<OrdnanceCasingComponent>(ammo.Entity.Value, out var casing))
                {
                    var ammoUid = ammo.Entity.Value;

                    var effectiveCasing = _ordnanceCasing.GetEffectiveCasing(ammoUid, casing, out var effectiveUid);
                    var hasTrigger = _ordnanceCasing.HasValidTrigger(effectiveUid, effectiveCasing);
                    var hasFuel = _ordnanceCasing.HasFuel(ammoUid, casing);

                    if (!hasFuel)
                    {
                        args.Ammo.RemoveAt(i);

                        if (hasTrigger)
                        {
                            DetonateFuelLessAmmo(ent.Owner, ammoUid, args.User);
                        }
                        else
                        {
                            if (args.User.HasValue)
                                _popup.PopupEntity(Loc.GetString("stories-ordnance-fizzle", ("casing", Name(ammoUid))), ent.Owner, args.User.Value, PopupType.Small);

                            _transform.SetCoordinates(ammoUid, _transform.GetMoverCoordinates(ent.Owner));
                        }
                    }
                    else
                    {
                        _ordnanceCasing.TryConsumeFuel(ammoUid, casing);
                    }
                }
            }
        }
    }

    private void OnGunAmmoShot(Entity<GunComponent> ent, ref AmmoShotEvent args)
    {
        if (TryComp<OrdnanceCasingComponent>(ent.Owner, out var gunCasing) && _tag.HasTag(ent.Owner, LaunchTubeTag))
        {
            var effectiveCasing = _ordnanceCasing.GetEffectiveCasing(ent.Owner, gunCasing, out var effectiveUid);
            var hasTrigger = _ordnanceCasing.HasValidTrigger(effectiveUid, effectiveCasing);

            foreach (var proj in args.FiredProjectiles)
            {
                if (!hasTrigger)
                {
                    RemComp<ExplodeOnTriggerComponent>(proj);
                    RemComp<ExplosiveComponent>(proj);
                    RemComp<CMExplosionEffectComponent>(proj);
                    RemComp<TriggerOnCollideComponent>(proj);
                }
                else
                {
                    if (effectiveUid != ent.Owner)
                    {
                        RemComp<ExplodeOnTriggerComponent>(proj);
                        RemComp<ExplosiveComponent>(proj);
                        RemComp<CMExplosionEffectComponent>(proj);

                        var custom = EnsureComp<OrdnanceCustomWarheadProjectileComponent>(proj);
                        custom.LauncherUid = ent.Owner;
                        custom.WarheadUid = effectiveUid;

                        var projComp = EnsureComp<ProjectileComponent>(proj);
                        projComp.MaxFixedRange = gunCasing.WarheadMaxRange;

                        if (TryComp<FixturesComponent>(proj, out var fixtures))
                        {
                            foreach (var (fixtureId, fixture) in fixtures.Fixtures)
                            {
                                _physics.SetCollisionMask(proj, fixtureId, fixture, fixture.CollisionMask | (int)CollisionGroup.Impassable | (int)CollisionGroup.BulletImpassable);
                                _physics.SetHard(proj, fixture, false);

                                var trigger = EnsureComp<TriggerOnCollideComponent>(proj);
                                trigger.FixtureID = fixtureId;
                                break;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var proj in args.FiredProjectiles)
            {
                if (TryComp<OrdnanceCasingComponent>(proj, out var casing))
                {
                    var effectiveCasing = _ordnanceCasing.GetEffectiveCasing(proj, casing, out var effectiveUid);
                    var hasTrigger = _ordnanceCasing.HasValidTrigger(effectiveUid, effectiveCasing);

                    if (hasTrigger)
                    {
                        var projComp = EnsureComp<ProjectileComponent>(proj);
                        projComp.MaxFixedRange = casing.WarheadMaxRange;

                        EnsureComp<ExplodeOnTriggerComponent>(proj);

                        if (TryComp<FixturesComponent>(proj, out var fixtures))
                        {
                            foreach (var (fixtureId, fixture) in fixtures.Fixtures)
                            {
                                _physics.SetCollisionMask(proj, fixtureId, fixture, fixture.CollisionMask | (int)CollisionGroup.Impassable | (int)CollisionGroup.BulletImpassable);
                                _physics.SetHard(proj, fixture, false);

                                var trigger = EnsureComp<TriggerOnCollideComponent>(proj);
                                trigger.FixtureID = fixtureId;
                                break;
                            }
                        }
                    }
                    else
                    {
                        RemComp<OrdnanceNoFuelDetonateComponent>(proj);
                        RemComp<TriggerOnCollideComponent>(proj);
                    }
                }
            }
        }
    }

    private void DetonateFuelLessAmmo(EntityUid gun, EntityUid ammo, EntityUid? user)
    {
        if (user.HasValue)
            _popup.PopupEntity(Loc.GetString("stories-ordnance-no-fuel-detonation"), gun, user.Value, PopupType.LargeCaution);

        var evDetonate = new OrdnanceDetonateEvent(user);
        RaiseLocalEvent(ammo, ref evDetonate);
    }
}
