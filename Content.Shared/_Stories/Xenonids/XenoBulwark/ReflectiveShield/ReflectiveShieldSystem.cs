using System.Linq;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Projectiles.Reflect;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Projectiles;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.ReflectiveShield;

public sealed class ReflectiveShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly RMCReflectSystem _reflect = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReflectiveShieldComponent, ReflectiveShieldActionEvent>(OnReflectiveShieldAction);
        SubscribeLocalEvent<ReflectiveShieldComponent, ChangeDirectionAttemptEvent>(OnChangeDirectionAttempt);
        SubscribeLocalEvent<ReflectiveShieldComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ReflectiveShieldComponent, ToggleCombatActionEvent>(OnCombatModeToggle);
        SubscribeLocalEvent<ReflectiveShieldComponent, ComponentShutdown>(OnReflectiveShieldShutdown);
        SubscribeLocalEvent<ReflectiveShieldComponent, PreventCollideEvent>(OnShieldPreventCollide, before: [typeof(RMCProjectileSystem)]);
        SubscribeLocalEvent<ReflectiveShieldComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<ReflectiveShieldComponent> xeno, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Deactivate(xeno);
    }

    private void OnShieldPreventCollide(Entity<ReflectiveShieldComponent> xeno, ref PreventCollideEvent args)
    {
        if (!xeno.Comp.Active)
            return;

        if (!HasComp<ProjectileComponent>(args.OtherEntity))
            return;

        var projUid = args.OtherEntity;

        if (HasComp<XenoProjectileComponent>(projUid))
            return;

        var meta = MetaData(projUid);
        if (meta.EntityPrototype != null &&
            xeno.Comp.PenetratingProjectiles.Contains(new EntProtoId(meta.EntityPrototype.ID)))
            return;

        if (TryComp(projUid, out RMCReflectedProjectileComponent? alreadyReflected) &&
            alreadyReflected.ReflectedBy.Contains(GetNetEntity(xeno).Id))
            return;

        if (TryComp(projUid, out Robust.Shared.Physics.Components.PhysicsComponent? projPhysics))
        {
            var velocity = _physics.GetMapLinearVelocity(projUid, projPhysics);
            if (velocity != System.Numerics.Vector2.Zero)
            {
                var xenoRot = _transform.GetWorldRotation(xeno);
                var projDirection = velocity.ToWorldAngle();
                var diff = Angle.ShortestDistance(xenoRot, projDirection.Opposite());

                if (Math.Abs(diff.Degrees) > xeno.Comp.FrontalAngle.Degrees)
                    return;
            }
        }

        if (!TryComp<ProjectileComponent>(projUid, out var projectileComp))
            return;

        if (!_reflect.TryReflectProjectile(projUid, xeno, xeno.Comp.ReflectAngle, xeno.Comp.ReflectChance))
            return;

        var reflected = EnsureComp<RMCReflectedProjectileComponent>(projUid);
        reflected.ReflectionMultiplier = xeno.Comp.ReflectionMultiplier;
        reflected.ReflectedBy.Add(GetNetEntity(xeno).Id);
        reflected.LastReflectedBy = GetNetEntity(xeno).Id;
        Dirty(projUid, reflected);

        projectileComp.IgnoreShooter = false;
        Dirty(projUid, projectileComp);

        RemComp<ProjectileIFFComponent>(projUid);

        args.Cancelled = true;
    }

    private void OnReflectiveShieldShutdown(Entity<ReflectiveShieldComponent> xeno, ref ComponentShutdown args)
    {
        if (!xeno.Comp.Active)
            return;

        RemComp<RMCReflectiveComponent>(xeno);
        RemComp<AuraComponent>(xeno);
        RemComp<MouseRotatorComponent>(xeno);
        RemComp<NoRotateOnMoveComponent>(xeno);

        xeno.Comp.Active = false;
        xeno.Comp.DeactivateAt = null;
        xeno.Comp.ActivatedAt = null;
        xeno.Comp.PendingCooldown = null;

        if (TryComp<CombatModeComponent>(xeno, out var combatMode) && combatMode.IsInCombatMode)
        {
            EnsureComp<MouseRotatorComponent>(xeno);
            EnsureComp<NoRotateOnMoveComponent>(xeno);
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ReflectiveShieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active && comp.PendingCooldown != null)
            {
                var pending = comp.PendingCooldown.Value;
                comp.PendingCooldown = null;
                Dirty(uid, comp);
                foreach (var action in _rmcActions.GetActionsWithEvent<ReflectiveShieldActionEvent>(uid))
                {
                    _actions.SetCooldown(action.Owner, _timing.CurTime, _timing.CurTime + pending);
                }
                continue;
            }

            if (!comp.Active || comp.DeactivateAt == null)
                continue;

            if (_timing.CurTime < comp.DeactivateAt)
                continue;

            comp.DeactivateAt = null;
            Deactivate((uid, comp));
        }
    }

    private void OnChangeDirectionAttempt(Entity<ReflectiveShieldComponent> xeno, ref ChangeDirectionAttemptEvent args)
    {
        if (xeno.Comp.Active)
            args.Cancel();
    }

    private void OnAttackAttempt(Entity<ReflectiveShieldComponent> xeno, ref AttackAttemptEvent args)
    {
        if (xeno.Comp.Active)
            args.Cancel();
    }

    private void OnCombatModeToggle(Entity<ReflectiveShieldComponent> xeno, ref ToggleCombatActionEvent args)
    {
        if (!xeno.Comp.Active)
            return;

        EnsureComp<MouseRotatorComponent>(xeno);
        EnsureComp<NoRotateOnMoveComponent>(xeno);
    }

    private void OnReflectiveShieldAction(Entity<ReflectiveShieldComponent> xeno, ref ReflectiveShieldActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EncasedPlates.EncasedPlatesComponent>(xeno, out var encased) || !encased.Active)
        {
            _popup.PopupClient(Loc.GetString("st-xeno-bulwark-reflective-shield-need-plates"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        if (xeno.Comp.Active)
        {
            if (xeno.Comp.ActivatedAt != null &&
                _timing.CurTime < xeno.Comp.ActivatedAt.Value + xeno.Comp.ToggleBuffer)
                return;

            args.Handled = true;
            Deactivate(xeno);
            return;
        }

        if (!TryComp<XenoPlasmaComponent>(xeno, out var plasma))
            return;

        if (!_xenoPlasma.HasPlasmaPopup((xeno, plasma), xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;
        _xenoPlasma.RemovePlasma((xeno, plasma), xeno.Comp.PlasmaCost);
        Activate(xeno);
    }

    private void Activate(Entity<ReflectiveShieldComponent> xeno)
    {
        xeno.Comp.Active = true;
        xeno.Comp.DeactivateAt = _timing.CurTime + xeno.Comp.Duration;
        xeno.Comp.ActivatedAt = _timing.CurTime;
        Dirty(xeno);

        var reflect = EnsureComp<RMCReflectiveComponent>(xeno);
        reflect.Chance = 0f;
        Dirty(xeno.Owner, reflect);

        _rmcPulling.TryStopAllPullsFromAndOn(xeno);
        _appearance.SetData(xeno, XenoVisualLayers.ReflectiveShield, true);
        _aura.GiveAura(xeno, new Color(0f, 1f, 1f), null);
        _popup.PopupClient(Loc.GetString("st-xeno-bulwark-reflective-shield-activate"), xeno, xeno, PopupType.Medium);

        EnsureComp<MouseRotatorComponent>(xeno);
        EnsureComp<NoRotateOnMoveComponent>(xeno);

        foreach (var action in _rmcActions.GetActionsWithEvent<ReflectiveShieldActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), true);
        }
    }

    private void Deactivate(Entity<ReflectiveShieldComponent> xeno)
    {
        if (!xeno.Comp.Active)
            return;

        TimeSpan cooldown;
        if (xeno.Comp.ActivatedAt != null)
        {
            var elapsed = _timing.CurTime - xeno.Comp.ActivatedAt.Value;
            var seconds = Math.Max(
                xeno.Comp.MinCooldown.TotalSeconds,
                elapsed.TotalSeconds * xeno.Comp.CooldownPerSecond);
            cooldown = TimeSpan.FromSeconds(seconds);
        }
        else
        {
            cooldown = xeno.Comp.FullCooldown;
        }

        xeno.Comp.Active = false;
        xeno.Comp.DeactivateAt = null;
        xeno.Comp.ActivatedAt = null;
        xeno.Comp.PendingCooldown = cooldown;
        Dirty(xeno);

        RemComp<RMCReflectiveComponent>(xeno);
        _appearance.SetData(xeno, XenoVisualLayers.ReflectiveShield, false);
        RemComp<AuraComponent>(xeno);
        _popup.PopupClient(Loc.GetString("st-xeno-bulwark-reflective-shield-deactivate"), xeno, xeno, PopupType.Small);

        RemComp<MouseRotatorComponent>(xeno);
        RemComp<NoRotateOnMoveComponent>(xeno);

        foreach (var action in _rmcActions.GetActionsWithEvent<ReflectiveShieldActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), false);
        }

        if (TryComp<CombatModeComponent>(xeno, out var combatMode) && combatMode.IsInCombatMode)
        {
            EnsureComp<MouseRotatorComponent>(xeno);
            EnsureComp<NoRotateOnMoveComponent>(xeno);
        }
    }
}
