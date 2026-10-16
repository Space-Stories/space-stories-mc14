using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerCausticEmbraceSystem : EntitySystem
{
    private const float TileHalfExtent = 0.5f;
    private const float UnobstructedRangeBuffer = 1f;
    private const float SplashUnobstructedRange = 2f;

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly XenoDespoilerCatalyzeFlagSystem _catalyze = default!;
    [Dependency] private readonly SharedXenoDespoilerAcidSystem _acid = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    private EntityQuery<XenoDespoilerLingeringAcidComponent> _lingeringQuery;

    public override void Initialize()
    {
        _lingeringQuery = GetEntityQuery<XenoDespoilerLingeringAcidComponent>();

        SubscribeLocalEvent<XenoDespoilerComponent, XenoDespoilerCausticEmbraceActionEvent>(OnUse);
    }

    private void OnUse(EntityUid uid, XenoDespoilerComponent comp, XenoDespoilerCausticEmbraceActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<XenoDespoilerCausticEmbraceActionComponent>(args.Action, out var action))
            return;

        var ownerXform = Transform(uid);
        var originMap = _xform.ToMapCoordinates(ownerXform.Coordinates);
        var targetMap = _xform.ToMapCoordinates(args.Target);
        if (originMap.MapId != targetMap.MapId)
            return;

        var toTarget = targetMap.Position - originMap.Position;
        var range = toTarget.Length();
        if (range < 0.01f)
            return;

        var facing = toTarget / range;

        if (_catalyze.IsEmpowered(uid, comp))
        {
            if (!CanEmpoweredLunge(uid, action, args, range, out var victim))
                return;

            if (!_rmcActions.TryUseAction(args))
                return;

            args.Handled = true;

            _xform.SetCoordinates(uid, Transform(victim.Value).Coordinates);

            if (action.PounceSound is { } empoweredSound)
                _audio.PlayPredicted(empoweredSound, uid, uid);

            if (_net.IsClient)
                return;

            ApplyEmpoweredHit(uid, action, victim.Value);
            _catalyze.TakeEmpowerment(uid, comp);
            return;
        }

        var landing = ownerXform.Coordinates.Offset(facing * action.NormalRange).SnapToGrid(EntityManager);
        var landingMap = _xform.ToMapCoordinates(landing);
        var landingDistance = (landingMap.Position - originMap.Position).Length();

        if (_rmcMap.IsTileBlocked(landing) ||
            !_interaction.InRangeUnobstructed(uid, landing, range: landingDistance + UnobstructedRangeBuffer))
        {
            _popup.PopupClient(Loc.GetString("st-xeno-despoiler-pounce-blocked"), uid, uid);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        _xform.SetCoordinates(uid, landing);

        if (action.PounceSound is { } sound)
            _audio.PlayPredicted(sound, uid, uid);

        if (_net.IsClient)
            return;

        SplashNeighborTiles(uid, action, landing, facing);
    }

    private static readonly Vector2i[] NeighborOffsets =
    {
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0), new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    };

    private static Vector2i FacingOffset(Vector2 dir)
    {
        return new Vector2i(Math.Sign(MathF.Round(dir.X)), Math.Sign(MathF.Round(dir.Y)));
    }

    private void SplashNeighborTiles(EntityUid caster,
        XenoDespoilerCausticEmbraceActionComponent action,
        EntityCoordinates center,
        Vector2 forward)
    {
        var behind = FacingOffset(-forward);

        var centerMap = _xform.ToMapCoordinates(center);
        var hits = _lookup.GetEntitiesIntersecting(centerMap.MapId,
            Box2.CenteredAround(centerMap.Position, new Vector2(action.SplashScanSize, action.SplashScanSize)));
        var damaged = new HashSet<EntityUid>();

        foreach (var offset in NeighborOffsets)
        {
            if (offset == behind)
                continue;

            var tile = center.Offset(new Vector2(offset.X, offset.Y));
            var tileMap = _xform.ToMapCoordinates(tile);

            if (!_interaction.InRangeUnobstructed(caster, tile, SplashUnobstructedRange))
                continue;

            var telegraph = Spawn(action.TelegraphProto, tile);
            _hive.SetSameHive(caster, telegraph);

            foreach (var ent in hits)
            {
                if (damaged.Contains(ent) || !_xeno.CanAbilityAttackTarget(caster, ent))
                    continue;

                var entPos = _xform.ToMapCoordinates(Transform(ent).Coordinates).Position;
                if (Math.Abs(entPos.X - tileMap.Position.X) > TileHalfExtent)
                    continue;
                if (Math.Abs(entPos.Y - tileMap.Position.Y) > TileHalfExtent)
                    continue;

                damaged.Add(ent);
                _damageable.TryChangeDamage(ent, action.SplashDamage, ignoreResistances: false, origin: caster);
            }

            if (_random.Prob(action.LingeringAcidChance))
            {
                var puddle = Spawn(action.LingeringAcidProto, tile);
                _hive.SetSameHive(caster, puddle);
                if (_lingeringQuery.TryComp(puddle, out var puddleComp))
                {
                    puddleComp.Caster = caster;
                    Dirty(puddle, puddleComp);
                }
            }
        }
    }

    private bool CanEmpoweredLunge(EntityUid uid,
        XenoDespoilerCausticEmbraceActionComponent action,
        XenoDespoilerCausticEmbraceActionEvent args,
        float dist,
        [NotNullWhen(true)] out EntityUid? victim)
    {
        victim = null;
        if (dist > action.EmpoweredRange)
        {
            _popup.PopupClient(Loc.GetString("st-xeno-despoiler-pounce-out-of-range"), uid, uid);
            return false;
        }

        victim = FindEmpoweredVictim(uid, args);
        if (victim is null)
        {
            _popup.PopupClient(Loc.GetString("st-xeno-despoiler-caustic-no-target"), uid, uid);
            return false;
        }

        if (!_interaction.InRangeUnobstructed(uid, victim.Value, range: action.EmpoweredRange + UnobstructedRangeBuffer))
        {
            _popup.PopupClient(Loc.GetString("st-xeno-despoiler-pounce-blocked"), uid, uid);
            victim = null;
            return false;
        }

        return true;
    }

    private void ApplyEmpoweredHit(EntityUid uid,
        XenoDespoilerCausticEmbraceActionComponent action,
        EntityUid victim)
    {
        _damageable.TryChangeDamage(victim, action.EmpoweredDamage, ignoreResistances: false, origin: uid);
        _acid.ApplyAcid(victim, uid, enhance: true);

        _stun.TryParalyze(victim, action.EmpoweredWeakenDuration, true);
    }

    private EntityUid? FindEmpoweredVictim(EntityUid caster, XenoDespoilerCausticEmbraceActionEvent args)
    {
        if (args.Entity is { } target && _xeno.CanAbilityAttackTarget(caster, target))
            return target;

        var landingMap = _xform.ToMapCoordinates(args.Target);
        foreach (var ent in _lookup.GetEntitiesIntersecting(landingMap.MapId,
                     Box2.CenteredAround(landingMap.Position, new Vector2(1f, 1f))))
        {
            if (_xeno.CanAbilityAttackTarget(caster, ent))
                return ent;
        }

        return null;
    }
}
