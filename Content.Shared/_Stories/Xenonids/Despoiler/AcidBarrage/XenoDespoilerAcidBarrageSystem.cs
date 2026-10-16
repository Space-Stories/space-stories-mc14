using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerAcidBarrageSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RMCProjectileSystem _rmcProjectile = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly XenoDespoilerCatalyzeFlagSystem _catalyze = default!;

    private EntityQuery<XenoDespoilerComponent> _despoilerQuery;
    private EntityQuery<XenoDespoilerArmedBarrageComponent> _armedQuery;
    private EntityQuery<XenoDespoilerChargingBarrageComponent> _chargingQuery;
    private EntityQuery<XenoDespoilerAcidBarrageProjectileComponent> _projectileQuery;

    public override void Initialize()
    {
        _despoilerQuery = GetEntityQuery<XenoDespoilerComponent>();
        _armedQuery = GetEntityQuery<XenoDespoilerArmedBarrageComponent>();
        _chargingQuery = GetEntityQuery<XenoDespoilerChargingBarrageComponent>();
        _projectileQuery = GetEntityQuery<XenoDespoilerAcidBarrageProjectileComponent>();

        SubscribeLocalEvent<XenoDespoilerComponent, XenoDespoilerAcidBarrageActionEvent>(OnAction);
        SubscribeAllEvent<XenoDespoilerBarrageStartChargeRequest>(OnStartChargeRequest);
        SubscribeAllEvent<XenoDespoilerBarrageFireRequest>(OnFireRequest);
        SubscribeAllEvent<XenoDespoilerBarrageCancelRequest>(OnCancelRequest);
    }

    private void OnCancelRequest(XenoDespoilerBarrageCancelRequest msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } uid &&
            (_armedQuery.HasComp(uid) || _chargingQuery.HasComp(uid)))
        {
            ResetBarrage(uid);
        }
    }

    private void OnAction(EntityUid uid, XenoDespoilerComponent comp, XenoDespoilerAcidBarrageActionEvent args)
    {
        if (args.Handled || !HasComp<XenoDespoilerAcidBarrageActionComponent>(args.Action))
            return;

        if (_armedQuery.HasComp(uid))
        {
            ResetBarrage(uid, args.Action);
            args.Handled = true;
            return;
        }

        EnsureComp<XenoDespoilerArmedBarrageComponent>(uid);
        _actions.SetToggled(args.Action.Owner, true);
        _popup.PopupClient(Loc.GetString("st-xeno-despoiler-barrage-armed"), uid, uid);
        args.Handled = true;
    }

    private void OnStartChargeRequest(XenoDespoilerBarrageStartChargeRequest msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (!_despoilerQuery.TryComp(uid, out var comp) || !_armedQuery.HasComp(uid) || _chargingQuery.HasComp(uid))
            return;

        if (!_actionBlocker.CanConsciouslyPerformAction(uid))
            return;

        if (!TryGetBarrageAction(uid, out var actionEnt, out var action))
            return;

        if (!_rmcActions.CanUseActionPopup(uid, actionEnt.Owner))
            return;

        var charging = EnsureComp<XenoDespoilerChargingBarrageComponent>(uid);
        charging.StartedAt = _timing.CurTime;
        charging.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(action.MaxChargeSeconds);
        charging.Empowered = _catalyze.IsEmpowered(uid, comp);
        charging.Target = msg.Target;
        charging.SpeedMultiplier = action.ChargingSpeedMultiplier;
        Dirty(uid, charging);

        if (action.ChargeSound is { } sound)
            _audio.PlayPredicted(sound, uid, uid);
    }

    private void OnFireRequest(XenoDespoilerBarrageFireRequest msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (!_despoilerQuery.TryComp(uid, out var comp))
            return;

        if (!_chargingQuery.TryComp(uid, out var charge))
            return;

        if (!_actionBlocker.CanConsciouslyPerformAction(uid))
        {
            ResetBarrage(uid);
            return;
        }

        var coords = GetCoordinates(msg.Target);
        if (!coords.IsValid(EntityManager))
            coords = GetCoordinates(charge.Target);

        if (!coords.IsValid(EntityManager))
        {
            ResetBarrage(uid);
            return;
        }

        var casterMap = _xform.ToMapCoordinates(Transform(uid).Coordinates);
        var targetMap = _xform.ToMapCoordinates(coords);
        if (casterMap.MapId != targetMap.MapId)
        {
            ResetBarrage(uid);
            return;
        }

        if (TryGetBarrageAction(uid, out var actionEnt, out var action) &&
            _rmcActions.TryUseAction(uid, actionEnt.Owner, uid))
        {
            FireVolley(uid, action, charge, coords);
            _actions.SetCooldown((actionEnt.Owner, null), action.PostFireCooldown);
            _catalyze.TakeEmpowerment(uid, comp);
        }

        ResetBarrage(uid);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoDespoilerChargingBarrageComponent>();
        while (query.MoveNext(out var uid, out var charge))
        {
            if (!_actionBlocker.CanConsciouslyPerformAction(uid))
            {
                ResetBarrage(uid);
                continue;
            }

            if (!TryGetBarrageAction(uid, out _, out var action) ||
                now >= charge.ExpiresAt + action.ChargeGracePeriod)
            {
                ResetBarrage(uid);
            }
        }
    }

    private void ResetBarrage(EntityUid uid, EntityUid? actionEnt = null)
    {
        RemCompDeferred<XenoDespoilerChargingBarrageComponent>(uid);
        RemCompDeferred<XenoDespoilerArmedBarrageComponent>(uid);

        if (actionEnt is null && TryGetBarrageAction(uid, out var found, out _))
            actionEnt = found.Owner;

        if (actionEnt is { } id)
            _actions.SetToggled(id, false);
    }

    private bool TryGetBarrageAction(EntityUid xeno,
        out Entity<ActionComponent> actionEnt,
        out XenoDespoilerAcidBarrageActionComponent action)
    {
        foreach (var entry in _rmcActions.GetActionsWithEvent<XenoDespoilerAcidBarrageActionEvent>(xeno))
        {
            if (!TryComp(entry.Owner, out XenoDespoilerAcidBarrageActionComponent? barrage))
                continue;

            actionEnt = entry;
            action = barrage;
            return true;
        }

        actionEnt = default;
        action = default!;
        return false;
    }

    private void FireVolley(EntityUid uid, XenoDespoilerAcidBarrageActionComponent action,
        XenoDespoilerChargingBarrageComponent charge, EntityCoordinates target)
    {
        if (_net.IsClient)
            return;

        var heldFor = (float)(_timing.CurTime - charge.StartedAt).TotalSeconds;
        var chargeFrac = Math.Clamp(heldFor / action.MaxChargeSeconds, 0f, 1f);

        var count = (int)MathF.Round(MathHelper.Lerp(action.MinProjectiles, action.MaxProjectiles, chargeFrac));
        count = Math.Clamp(count, action.MinProjectiles, action.MaxProjectiles);
        if (charge.Empowered)
            count += action.EmpowerBonusProjectiles;

        var casterCoords = Transform(uid).Coordinates;
        var casterMap = _xform.ToMapCoordinates(casterCoords);
        var targetMap = _xform.ToMapCoordinates(target);

        Angle baseAngle;
        if (casterMap.MapId == targetMap.MapId &&
            (targetMap.Position - casterMap.Position).LengthSquared() >= 0.0001f)
        {
            baseAngle = new Angle(targetMap.Position - casterMap.Position);
        }
        else
        {
            baseAngle = new Angle(Transform(uid).LocalRotation.ToWorldVec());
        }

        var scatter = Angle.FromDegrees(action.ScatterDegrees);
        var scaleSpan = action.MaxProjectileScale - action.MinProjectileScale;

        // Fan the shots evenly across [baseAngle - scatter, baseAngle + scatter] instead of picking each one at random.
        var arcStart = baseAngle - scatter;
        var arcStep = count > 1 ? scatter.Theta * 2d / (count - 1) : 0d;

        for (var i = 0; i < count; i++)
        {
            var shotAngle = count == 1 ? baseAngle : arcStart + new Angle(arcStep * i);
            var heading = shotAngle.ToVec();
            var shotRange = _random.Next(action.MinRangeTiles, action.MaxRangeTiles + 1);

            var proj = Spawn(action.ProjectileId, casterCoords);
            _hive.SetSameHive(uid, proj);

            if (_projectileQuery.TryComp(proj, out var projComp))
            {
                projComp.Shooter = uid;
                var scaleFactor = action.MinProjectileScale + (float)_random.NextDouble() * scaleSpan;
                projComp.Scale = new Vector2(scaleFactor, scaleFactor);
                Dirty(proj, projComp);
            }

            _rmcProjectile.SetMaxRange(proj, shotRange);
            _gun.ShootProjectile(proj, heading * shotRange, Vector2.Zero, uid, uid, speed: action.ProjectileSpeed);
        }

        if (action.FireSound is { } sound)
            _audio.PlayPvs(sound, uid);
    }
}
