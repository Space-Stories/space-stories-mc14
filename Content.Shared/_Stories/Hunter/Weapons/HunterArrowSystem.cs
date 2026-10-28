using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Hunter.Weapons;

public sealed class HunterArrowSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly RMCDazedSystem _dazed = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;
    [Dependency] private readonly RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterArrowComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HunterArrowComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HunterArrowComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<HunterArrowComponent, ProjectileEmbedEvent>(OnProjectileEmbed);
    }

    private void OnMapInit(Entity<HunterArrowComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<HunterArrowStartActiveComponent>(ent))
        {
            RemComp<HunterArrowStartActiveComponent>(ent);
            SetActive(ent, true);
            return;
        }

        _appearance.SetData(ent, HunterArrowVisuals.Active, ent.Comp.Active);
    }

    private void OnUseInHand(Entity<HunterArrowComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.ActiveComponents == null)
            return;

        args.Handled = true;

        var user = args.User;
        if (!_skills.HasSkill(user, ent.Comp.Skill, 1))
        {
            _popup.PopupClient(Loc.GetString("stories-hunter-arrow-no-skill"), user, user, PopupType.SmallCaution);
            return;
        }

        var active = !ent.Comp.Active;
        _popup.PopupClient(Loc.GetString(active ? ent.Comp.ActivatePopup : ent.Comp.DeactivatePopup), user, user);

        if (_net.IsServer)
            SetActive(ent, active);
    }

    private void SetActive(Entity<HunterArrowComponent> ent, bool active)
    {
        if (ent.Comp.ActiveComponents is { } activeComps)
        {
            if (active)
                EntityManager.AddComponents(ent, activeComps);
            else
                EntityManager.RemoveComponents(ent, activeComps);
        }

        if (!active && ent.Comp.InactiveComponents is { } inactiveComps)
            EntityManager.AddComponents(ent, inactiveComps);

        if (ent.Comp.ActiveTag is { } activeTag && ent.Comp.InactiveTag is { } inactiveTag)
        {
            _tag.RemoveTag(ent, active ? inactiveTag : activeTag);
            _tag.AddTag(ent, active ? activeTag : inactiveTag);
        }

        if ((active ? ent.Comp.ActiveName : ent.Comp.InactiveName) is { } name)
            _metaData.SetEntityName(ent, Loc.GetString(name));

        ent.Comp.Active = active;
        Dirty(ent);
        _appearance.SetData(ent, HunterArrowVisuals.Active, active);
    }

    private void OnProjectileHit(Entity<HunterArrowComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.Active)
            return;

        var target = args.Target;

        if (ent.Comp.StunTime is { } stun)
            _stun.TryStun(target, stun, true);

        if (ent.Comp.DazeTime is { } daze && _net.IsServer)
            _dazed.TryDaze(target, daze);

        if (ent.Comp.PushMaxRange is not { } pushRange || args.Shooter is not { } shooter || _standing.IsDown(target))
            return;

        var from = _transform.GetMapCoordinates(shooter);
        var to = _transform.GetMapCoordinates(target);
        if (from.MapId != to.MapId || (to.Position - from.Position).Length() > pushRange)
            return;

        _sizeStun.KnockBack(target, from, 1f, 1f);
    }

    private void OnProjectileEmbed(Entity<HunterArrowComponent> ent, ref ProjectileEmbedEvent args)
    {
        if (ent.Comp.Active || !ent.Comp.DetachFromNonHumans || IsHuman(args.Embedded))
            return;

        _projectile.EmbedDetach(ent, null);
    }

    private bool IsHuman(EntityUid target)
    {
        return HasComp<HumanoidAppearanceComponent>(target) && !HasComp<XenoComponent>(target);
    }
}
