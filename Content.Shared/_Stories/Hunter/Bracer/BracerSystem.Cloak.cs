using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Devour;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Xenonids.Predalien.PredalienRoar;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void InitializeCloak()
    {
        SubscribeLocalEvent<HunterBracerComponent, ToggleBracerCloakEvent>(OnToggleCloak);

        SubscribeLocalEvent<BracerCloakedComponent, VaporHitEvent>(OnVaporHit);
        SubscribeLocalEvent<BracerCloakedComponent, MobStateChangedEvent>(OnCloakedMobStateChanged);
        SubscribeLocalEvent<BracerCloakedComponent, XenoDevouredEvent>(OnDevoured);
        SubscribeLocalEvent<BracerCloakedComponent, XenoParasiteInfectEvent>(OnParasiteInfected);
        SubscribeLocalEvent<BracerCloakedComponent, STPredalienRevealEvent>(OnPredalienReveal);
        SubscribeLocalEvent<BracerCloakedComponent, ProjectileHitEvent>(OnProjectileHit);

        SubscribeLocalEvent<BracerCloakedComponent, AttemptShootEvent>(OnAttemptShootCloaked);
        SubscribeLocalEvent<BracerRecentlyUncloakedComponent, AttemptShootEvent>(OnAttemptShootUncloaked);
        SubscribeLocalEvent<BracerCancelUseWithCloakComponent, UseInHandEvent>(
            OnAttemptUseCloaked,
            new[] { typeof(SharedGunSystem) }
        );
    }

    private void UpdateCloak()
    {
        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;

        var queryUncloak = EntityQueryEnumerator<BracerRecentlyUncloakedComponent>();
        while (queryUncloak.MoveNext(out var uid, out var comp))
        {
            if (comp.ExpireTime <= curTime)
                RemComp<BracerRecentlyUncloakedComponent>(uid);
        }

        var queryCloak = EntityQueryEnumerator<BracerCloakedComponent, TransformComponent>();
        while (queryCloak.MoveNext(out var uid, out _, out var xform))
        {
            if (!TryFindWornBracer(uid, out var bracer))
            {
                ForceUncloak(uid);
                continue;
            }

            if (IsAuthorized(uid, bracer.Value.Comp))
                continue;

            if (_random.NextFloat() < 0.05f)
            {
                PunishUser(uid, bracer.Value);
                ForceUncloak(uid);
            }
        }
    }

    private void OnToggleCloak(Entity<HunterBracerComponent> ent, ref ToggleBracerCloakEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var shouldBeCloaked = !HasComp<BracerCloakedComponent>(user);

        if (shouldBeCloaked)
            if (!AttemptUsage(user, ent))
                return;

        if (_net.IsClient)
            return;

        args.Handled = true;
        SetCloak(user, ent, shouldBeCloaked, false);
    }

    public void SetCloak(EntityUid user,
        Entity<HunterBracerComponent> bracer,
        bool enabled,
        bool forced,
        bool quiet = false)
    {
        if (!TryComp<EntityTurnInvisibleComponent>(user, out var turnInvisible))
            return;

        var isCloaked = HasComp<BracerCloakedComponent>(user);

        if (enabled)
        {
            if (isCloaked)
                return;

            if (HasComp<BracerSelfDestructingComponent>(bracer))
            {
                _popup.PopupEntity(Loc.GetString("st-bracer-cloak-fail-self-destruct"), user, user);
                return;
            }

            if (!TryDrainPower(user, bracer, bracer.Comp.CloakPowerCost))
                return;

            AddComp<BracerCloakedComponent>(user);
            var activeInvisibility = EnsureComp<EntityActiveInvisibleComponent>(user);
            activeInvisibility.Opacity = bracer.Comp.CloakOpacity;
            Dirty(user, activeInvisibility);

            turnInvisible.Enabled = true;
            turnInvisible.UncloakTime = _timing.CurTime;

            if (_net.IsServer)
                Spawn(bracer.Comp.CloakEffect, Transform(user).Coordinates);

            _audio.PlayPvs(bracer.Comp.CloakOnSound, user);

            if (!quiet)
                _popup.PopupEntity(Loc.GetString("st-bracer-cloak-on"), user, user);
        }
        else
        {
            if (!isCloaked)
                return;

            RemComp<BracerCloakedComponent>(user);

            if (TryComp<EntityActiveInvisibleComponent>(user, out var invisible))
            {
                invisible.Opacity = 1f;
                Dirty(user, invisible);
                RemCompDeferred<EntityActiveInvisibleComponent>(user);
            }

            turnInvisible.Enabled = false;
            turnInvisible.UncloakTime = _timing.CurTime;

            if (bracer.Comp.RestrictWeaponsOnCloak)
            {
                var recentlyUncloaked = EnsureComp<BracerRecentlyUncloakedComponent>(user);
                recentlyUncloaked.ExpireTime = _timing.CurTime + bracer.Comp.UncloakWeaponLockDuration;
                Dirty(user, recentlyUncloaked);
            }

            if (_net.IsServer)
                Spawn(bracer.Comp.UncloakEffect, Transform(user).Coordinates);

            if (!quiet)
            {
                _audio.PlayPvs(bracer.Comp.CloakOffSound, user);

                _popup.PopupEntity(
                    forced ? Loc.GetString("st-bracer-cloak-off-forced") : Loc.GetString("st-bracer-cloak-off"),
                    user,
                    user
                );
            }
        }

        if (bracer.Comp.ToggleCloakAction is { } action)
            _actions.SetToggled(action, enabled);
    }

    private void ForceUncloak(EntityUid user)
    {
        if (!TryFindWornBracer(user, out var bracer))
            return;

        SetCloak(user, bracer.Value, false, true);
    }

    private bool TryFindWornBracer(EntityUid user, [NotNullWhen(true)] out Entity<HunterBracerComponent>? bracer)
    {
        bracer = null;
        if (
            !_inventory.TryGetSlotEntity(user, "gloves", out var equipped)
            || !TryComp<HunterBracerComponent>(equipped, out var bracerComp)
        )
            return false;
        bracer = (equipped.Value, bracerComp);
        return true;
    }

    private void OnVaporHit(Entity<BracerCloakedComponent> ent, ref VaporHitEvent args)
    {
        ForceUncloak(ent.Owner);
    }

    private void OnCloakedMobStateChanged(Entity<BracerCloakedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState > MobState.Critical)
            ForceUncloak(ent.Owner);
    }

    private void OnDevoured(Entity<BracerCloakedComponent> ent, ref XenoDevouredEvent args)
    {
        ForceUncloak(ent.Owner);
    }

    private void OnParasiteInfected(Entity<BracerCloakedComponent> ent, ref XenoParasiteInfectEvent args)
    {
        ForceUncloak(ent.Owner);
    }

    private void OnPredalienReveal(Entity<BracerCloakedComponent> ent, ref STPredalienRevealEvent args)
    {
        if (!TryFindWornBracer(ent.Owner, out var bracer))
            return;

        SetCloak(ent.Owner, bracer.Value, false, true, true);

        if (bracer.Value.Comp.ToggleCloakAction is { } action)
            _actions.SetIfBiggerCooldown(action, args.RecloakPenalty);
    }

    private void OnProjectileHit(Entity<BracerCloakedComponent> ent, ref ProjectileHitEvent args)
    {
        ForceUncloak(ent.Owner);
    }

    private void OnAttemptShootCloaked(Entity<BracerCloakedComponent> ent, ref AttemptShootEvent args)
    {
        if (!TryFindWornBracer(ent.Owner, out var bracer) || !bracer.Value.Comp.RestrictWeaponsOnCloak)
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("st-bracer-cloak-cannot-shoot"), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }

    private void OnAttemptShootUncloaked(Entity<BracerRecentlyUncloakedComponent> ent, ref AttemptShootEvent args)
    {
        if (!TryFindWornBracer(ent.Owner, out var bracer) || !bracer.Value.Comp.RestrictWeaponsOnCloak)
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("st-bracer-cloak-cannot-shoot"), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }

    private void OnAttemptUseCloaked(Entity<BracerCancelUseWithCloakComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<BracerCloakedComponent>(args.User) || HasComp<BracerRecentlyUncloakedComponent>(args.User))
        {
            if (TryFindWornBracer(args.User, out var bracer) && bracer.Value.Comp.RestrictWeaponsOnCloak)
            {
                args.Handled = true;
                _popup.PopupClient(Loc.GetString(ent.Comp.CancelMessage), args.User, args.User, PopupType.SmallCaution);
            }
        }
    }
}
