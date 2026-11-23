using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared._Stories.Ordnance;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Toggleable;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Explosion;

public abstract partial class SharedRMCLandmineSystem : EntitySystem
{
    [Dependency] protected readonly GunIFFSystem GunIff = default!;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CollisionWakeSystem _collisionWake = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    // Stories-Ordnance-Start
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;
    // Stories-Ordnance-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCLandmineComponent, ClaymoreDeployDoafterEvent>(OnClaymoreDeploy);
        SubscribeLocalEvent<RMCLandmineComponent, ClaymoreDisarmDoafterEvent>(OnClaymoreDisarm);
        SubscribeLocalEvent<RMCLandmineComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RMCLandmineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RMCLandmineComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<RMCLandmineComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<RMCLandmineComponent, CombatModeShouldHandInteractEvent>(OnShouldInteract);
    }

    private void OnClaymoreDeploy(Entity<RMCLandmineComponent> ent, ref ClaymoreDeployDoafterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!CanDeployPopup(ent, args.User, out var coordinates, out var rotation))
            return;

        var xform = Transform(ent);
        // Stories-Ordnance-Start
        _transform.SetCoordinates(ent.Owner, xform, coordinates);
        _transform.SetLocalRotation(ent.Owner, rotation);
        // Stories-Ordnance-End
        _transform.AnchorEntity(ent, xform);
        _collisionWake.SetEnabled(ent, false);
        _physics.SetBodyType(ent, BodyType.Static);

        GunIff.TryGetFaction(args.User, out var faction);
        ent.Comp.Faction = faction;
        ent.Comp.Armed = true;

        UpdateAppearance(ent);
        _audio.PlayPredicted(ent.Comp.DeploySound, ent, args.User);
    }

    private void OnClaymoreDisarm(Entity<RMCLandmineComponent> ent, ref ClaymoreDisarmDoafterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Stories-Ordnance-Start
        if (ent.Comp.Faction != null && !GunIff.IsInFaction(args.User, ent.Comp.Faction.Value))
        {
            if (_random.Prob(0.75f))
            {
                _popup.PopupClient(Loc.GetString("stories-mine-defuse-fail"), ent, args.User, PopupType.LargeCaution);
                if (_net.IsServer)
                {
                    var failEv = new RMCLandmineDefuseFailEvent(args.User);
                    RaiseLocalEvent(ent, ref failEv);
                }
                return;
            }
        }
        // Stories-Ordnance-End

        _transform.Unanchor(ent);
        _collisionWake.SetEnabled(ent, true);
        ent.Comp.Armed = false;
        _physics.SetBodyType(ent, BodyType.Dynamic);

        if (TryComp(args.User, out HandsComponent? hands))
            _hands.TryPickupAnyHand(args.User, ent, handsComp: hands);

        UpdateAppearance(ent);
        _popup.PopupClient(Loc.GetString("stories-ordnance-defuse-success"), ent, args.User); // Stories-Ordnance
    }

    private void OnUseInHand(Entity<RMCLandmineComponent> ent, ref UseInHandEvent args)
    {
        // Stories-Ordnance-Start
        if (args.Handled)
            return;

        if (ent.Comp.Armed)
            return;
        // Stories-Ordnance-End

        if (!CanDeployPopup(ent, args.User, out _, out _))
            return;

        args.Handled = true; // Stories-Ordnance

        var doAfterArgs = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.PlacementDelay,
            new ClaymoreDeployDoafterEvent(),
            ent,
            ent,
            args.User)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(new DoAfterArgs(doAfterArgs));
    }

    private void OnInteractUsing(Entity<RMCLandmineComponent> ent, ref InteractUsingEvent args)
    {
        // Stories-Ordnance-Start
        if (args.Handled)
            return;

        if (!ent.Comp.Armed)
            return;
        // Stories-Ordnance-End

        if (!_tool.HasQuality(args.Used, ent.Comp.DisarmTool))
            return;

        args.Handled = true; // Stories-Ordnance

        var doAfterArgs = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.DisarmDelay,
            new ClaymoreDisarmDoafterEvent(),
            ent,
            ent,
            args.Used) // Stories-Ordnance
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(new DoAfterArgs(doAfterArgs));
    }

    private void OnPreventCollide(Entity<RMCLandmineComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Armed && !HasComp<XenoProjectileComponent>(args.OtherEntity) && !HasComp<MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnBeforeDamageChanged(Entity<RMCLandmineComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (HasComp<ProjectileComponent>(args.Source))
            args.Cancelled = true;

        if (!ent.Comp.Armed)
            args.Cancelled = true;
    }

    private void OnShouldInteract(Entity<RMCLandmineComponent> ent, ref CombatModeShouldHandInteractEvent args)
    {
        if (HasComp<XenoComponent>(args.User))
            args.Cancelled = true;
    }

    private bool CanDeployPopup(Entity<RMCLandmineComponent> ent,
        EntityUid user,
        out EntityCoordinates coordinates,
        out Angle rotation)
    {
        // Stories-Ordnance-Start
        var moverCoordinates = _transform.GetMoverCoordinates(user);
        coordinates = moverCoordinates;

        var worldRot = _transform.GetWorldRotation(user);
        rotation = worldRot.GetCardinalDir().ToAngle();
        // Stories-Ordnance-End

        // Can't deploy a mine while inside a container
        if (_container.IsEntityInContainer(user))
        {
            var msg = Loc.GetString("rmc-explosive-deploy-container", ("explosive", ent));
            _popup.PopupClient(msg, user, user, PopupType.SmallCaution);
            return false;
        }

        // Stories-VehicleDeployBlock-Start
        if (HasComp<VehicleInteriorOccupantComponent>(user) ||
            (Transform(user).MapUid is { } mapUid && HasComp<VehicleInteriorLinkComponent>(mapUid)))
        {
            _popup.PopupClient(Loc.GetString("emplacement-mount-deploy-vehicle"), user, user, PopupType.SmallCaution);
            return false;
        }
        // Stories-VehicleDeployBlock-End

        // Can't deploy a mine on a tile that already has a mine on it
        var query = _rmcMap.GetAnchoredEntitiesEnumerator(moverCoordinates); // Stories-Ordnance
        while (query.MoveNext(out var anchoredUid))
        {
            if (!HasComp<RMCLandmineComponent>(anchoredUid))
                continue;

            var msg = Loc.GetString("rmc-mine-deploy-fail-occupied");
            _popup.PopupClient(msg, user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private void UpdateAppearance(Entity<RMCLandmineComponent> ent)
    {
        _appearance.SetData(ent, ToggleableVisuals.Enabled, ent.Comp.Armed);

        // Stories-Ordnance-Start
        if (TryComp<OrdnanceCasingComponent>(ent, out var casing))
        {
            var casingSys = EntityManager.System<SharedOrdnanceCasingSystem>();
            casingSys.UpdateAppearance((ent.Owner, casing));
        }
        // Stories-Ordnance-End
    }
}

/// <summary>
///     DoAfter event for placing the mine.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ClaymoreDeployDoafterEvent : SimpleDoAfterEvent
{

}

/// <summary>
///     DoAfter event for disarming the mine.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ClaymoreDisarmDoafterEvent : SimpleDoAfterEvent
{

}

[ByRefEvent]
public readonly record struct RMCLandmineDefuseFailEvent(EntityUid User);
