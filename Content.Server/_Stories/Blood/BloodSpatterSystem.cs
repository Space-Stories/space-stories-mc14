using System.Numerics;
using Content.Shared._Stories.Blood;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Decals;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stories.Blood;

public sealed class BloodSpatterSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";

    private static readonly ProtoId<DecalPrototype>[] FloorSplatterDecals =
    [
        "STDecalBloodSplatter1",
        "STDecalBloodSplatter2",
        "STDecalBloodSplatter3",
        "STDecalBloodSplatter4",
        "STDecalBloodSplatter5",
        "STDecalBloodSplatter6",
        "STDecalBloodSplatter7",
    ];

    private static readonly EntProtoId SplatterEffect = "STBloodSplatterEffect";

    [Dependency] private readonly BloodStainSystem _bloodStain = default!;
    [Dependency] private readonly BloodTracksSystem _bloodTracks = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodyComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<BloodyComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            args.DamageDelta is not { } delta ||
            !_prototype.TryIndex(BruteGroup, out var bruteGroup) ||
            !delta.TryGetDamageInGroup(bruteGroup, out var brute) ||
            brute <= 0 ||
            !_bloodStain.TryGetBloodColor(ent, out var color))
        {
            return;
        }

        var melee = args.Tool is { } tool && HasComp<MeleeWeaponComponent>(tool);
        if (!_container.IsEntityInContainer(ent))
        {
            var direction = GetHitDirection(ent, args.Origin);
            SpawnSplatterEffect(ent, color, direction);

            if (_random.Prob(melee ? ent.Comp.MeleeFloorChance : ent.Comp.RangedFloorChance))
                AddFloorSplatter(ent, color, direction);
        }

        var chance = Math.Clamp(ent.Comp.BaseStainChance + ent.Comp.StainChancePerDamage * brute.Float(), 0f, 1f);
        if (!_random.Prob(chance))
            return;

        if (melee && args.Tool != args.Origin && HasComp<ItemComponent>(args.Tool))
            _bloodStain.TryStain(args.Tool!.Value, color);

        if (!_random.Prob(ent.Comp.SplashChance))
            return;

        var parts = _random.Prob(ent.Comp.HeadSplashChance) ? BloodStainParts.Head : BloodStainParts.Body;
        _bloodStain.StainWearer(ent.AsNullable(), parts, color);

        if (melee &&
            args.Origin is { } origin &&
            origin != ent.Owner &&
            _transform.InRange(Transform(origin).Coordinates, Transform(ent).Coordinates, ent.Comp.MeleeContactRange))
        {
            _bloodStain.StainWearer(origin, BloodStainParts.Body | BloodStainParts.Hands, color);
        }
    }

    private Angle GetHitDirection(EntityUid target, EntityUid? origin)
    {
        if (origin is { } source && source != target && !TerminatingOrDeleted(source))
        {
            var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(source);
            if (delta.LengthSquared() > 0.0001f)
                return delta.ToWorldAngle();
        }

        return _random.NextAngle();
    }

    private void SpawnSplatterEffect(EntityUid target, Color color, Angle direction)
    {
        var effect = Spawn(SplatterEffect, _transform.GetMapCoordinates(target));
        var comp = EnsureComp<BloodSplatterEffectComponent>(effect);
        comp.Color = color;
        comp.Direction = direction;
        comp.Variant = _random.Next(comp.States.Count);
        Dirty(effect, comp);
    }

    private void AddFloorSplatter(Entity<BloodyComponent> target, Color color, Angle direction)
    {
        var xform = Transform(target);
        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var worldPosition = _transform.GetWorldPosition(xform) + direction.RotateVec(new Vector2(0, -target.Comp.FloorSplatterShift));
        var local = Vector2.Transform(worldPosition, _transform.GetInvWorldMatrix(gridUid));
        _bloodTracks.TryAddSplatter(_random.Pick(FloorSplatterDecals), gridUid, grid, local, color, _random.NextAngle());
    }
}
