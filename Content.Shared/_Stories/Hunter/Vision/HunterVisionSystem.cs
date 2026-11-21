using Content.Shared._RMC14.Mobs;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Scoping;
using Content.Shared._Stories.Hunter.Bracer;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Hunter.Vision;

public sealed class HunterVisionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BracerSystem _bracer = default!;
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterVisionMaskComponent, ToggleHunterVisionEvent>(OnToggleVision);

        SubscribeLocalEvent<HunterVisionMaskComponent, GotEquippedEvent>(OnMaskEquipped);
        SubscribeLocalEvent<HunterVisionMaskComponent, GotUnequippedEvent>(OnMaskUnequipped);

        SubscribeLocalEvent<HunterVisionMaskComponent, GetItemActionsEvent>(
            OnGetActions,
            after: new[] { typeof(SharedScopeSystem) }
        );
    }

    private void OnGetActions(Entity<HunterVisionMaskComponent> ent, ref GetItemActionsEvent args)
    {
        if (!TryComp<ScopeComponent>(ent, out var scope) || scope.ScopingToggleActionEntity == null)
            return;

        var shouldRemove = false;

        if (!HasComp<HunterComponent>(args.User))
            shouldRemove = true;
        else if (!_inventory.TryGetSlotEntity(args.User, "mask", out var wornMask) || wornMask != ent.Owner)
            shouldRemove = true;
        else if (
            !_bracer.IsHunterWithBracer(args.User, out var bracer)
            || !_bracer.IsAuthorized(args.User, bracer.Value.Comp)
        )
            shouldRemove = true;

        if (shouldRemove)
        {
            args.Actions.Remove(scope.ScopingToggleActionEntity.Value);
        }
    }

    private void OnMaskEquipped(Entity<HunterVisionMaskComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != "mask")
            return;

        if (_net.IsServer)
            UpdateMaskAbilities(ent, args.Equipee);

        if (ent.Comp.IsActive)
            EnableVisionEffects(args.Equipee, ent.Comp);
    }

    private void OnMaskUnequipped(Entity<HunterVisionMaskComponent> ent, ref GotUnequippedEvent args)
    {
        if (_net.IsServer)
            RemoveMaskAbilities(ent, args.Equipee);
    }

    public void HandleBracerEquipped(EntityUid user)
    {
        if (_net.IsClient)
            return;

        if (
            _inventory.TryGetSlotEntity(user, "mask", out var maskUid)
            && TryComp<HunterVisionMaskComponent>(maskUid, out var maskComp)
        )
            UpdateMaskAbilities((maskUid.Value, maskComp), user);
    }

    public void HandleBracerUnequipped(EntityUid user)
    {
        if (_net.IsClient)
            return;

        if (
            _inventory.TryGetSlotEntity(user, "mask", out var maskUid)
            && TryComp<HunterVisionMaskComponent>(maskUid, out var maskComp)
        )
        {
            UpdateMaskAbilities((maskUid.Value, maskComp), user);
        }
    }

    private void UpdateMaskAbilities(Entity<HunterVisionMaskComponent> mask, EntityUid user)
    {
        if (!HasComp<HunterComponent>(user))
            return;

        EnsureComp<ShowHealthIconsComponent>(user);
        EnsureComp<ShowHealthBarsComponent>(user);
        EnsureComp<CMGhostXenoHudComponent>(user);

        var hasBracer =
            _bracer.IsHunterWithBracer(user, out var bracer) && _bracer.IsAuthorized(user, bracer.Value.Comp);

        if (hasBracer)
        {
            _actions.AddAction(user, ref mask.Comp.ToggleVisionAction, mask.Comp.ToggleVisionActionId, mask.Owner);

            if (
                mask.Comp.ToggleVisionAction != null
                && TryComp(mask.Comp.ToggleVisionAction, out ActionComponent? actionComp)
            )
                _actions.UpdateAction((mask.Comp.ToggleVisionAction.Value, actionComp));

            if (TryComp<ScopeComponent>(mask, out var scope))
                _actions.AddAction(user, ref scope.ScopingToggleActionEntity, scope.ScopingToggleAction, mask.Owner);
        }
        else
            RemoveMaskAbilities(mask, user);
    }

    private void RemoveMaskAbilities(Entity<HunterVisionMaskComponent> mask, EntityUid user)
    {
        RemComp<ShowHealthIconsComponent>(user);
        RemComp<ShowHealthBarsComponent>(user);
        RemComp<CMGhostXenoHudComponent>(user);

        RemoveActionsOnly(mask, user);
        DisableVisionEffects(user);

        if (TryComp<ScopingComponent>(user, out var scoping) && scoping.Scope == mask.Owner)
        {
            _contentEye.ResetZoom(user);
            RemComp<ScopingComponent>(user);
        }
    }

    private void RemoveActionsOnly(Entity<HunterVisionMaskComponent> mask, EntityUid user)
    {
        if (mask.Comp.ToggleVisionAction != null && Exists(mask.Comp.ToggleVisionAction))
        {
            if (TryComp(mask.Comp.ToggleVisionAction, out ActionComponent? action) && action.AttachedEntity == user)
                _actions.RemoveAction(user, mask.Comp.ToggleVisionAction.Value);
            
            mask.Comp.ToggleVisionAction = null;
            Dirty(mask);
        }

        if (TryComp<ScopeComponent>(mask, out var scope) && scope.ScopingToggleActionEntity != null &&
            Exists(scope.ScopingToggleActionEntity))
        {
            if (TryComp(scope.ScopingToggleActionEntity, out ActionComponent? action) && action.AttachedEntity == user)
                _actions.RemoveAction(user, scope.ScopingToggleActionEntity.Value);
            
            scope.ScopingToggleActionEntity = null;
            Dirty(mask);
        }
    }

    private void OnToggleVision(Entity<HunterVisionMaskComponent> ent, ref ToggleHunterVisionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (!HasComp<HunterComponent>(user))
            return;

        if (!_bracer.IsHunterWithBracer(user, out var bracer))
        {
            _popup.PopupClient(Loc.GetString("st-hunter-vision-no-bracer"), user, user);
            return;
        }

        if (!_bracer.AttemptUsage(user, bracer.Value))
            return;

        args.Handled = true;
        var mask = ent.Comp;

        mask.IsActive = !mask.IsActive;
        Dirty(ent);

        _audio.PlayPredicted(mask.VisorSwitchSound, ent, user);

        if (mask.IsActive)
        {
            if (bracer.Value.Comp.Charge < mask.EnergyUsage)
            {
                mask.IsActive = false;
                Dirty(ent);
                _popup.PopupClient(Loc.GetString("st-bracer-no-power"), user, user);
                return;
            }

            EnableVisionEffects(user, mask);
            _popup.PopupClient(Loc.GetString("st-hunter-vision-on"), user, user);
        }
        else
        {
            DisableVisionEffects(user);
            _popup.PopupClient(Loc.GetString("st-hunter-vision-off"), user, user);
        }

        if (mask.ToggleVisionAction != null)
            _actions.SetToggled(mask.ToggleVisionAction.Value, mask.IsActive);
    }

    private void EnableVisionEffects(EntityUid user, HunterVisionMaskComponent mask)
    {
        if (_net.IsClient)
            return;

        var userNv = EnsureComp<NightVisionComponent>(user);

        userNv.State = NightVisionState.Half;
        userNv.Overlay = true;
        userNv.SeeThroughContainers = true;
        userNv.Innate = false;
        userNv.Green = false;

        Dirty(user, userNv);
    }

    private void DisableVisionEffects(EntityUid user)
    {
        if (_net.IsClient)
            return;

        if (TryComp<NightVisionComponent>(user, out var userNv) && !userNv.Innate)
            RemComp<NightVisionComponent>(user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<HunterVisionMaskComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mask, out var xform))
        {
            if (!mask.IsActive)
                continue;

            if (xform.ParentUid == EntityUid.Invalid)
            {
                mask.IsActive = false;
                Dirty(uid, mask);
                continue;
            }

            var user = xform.ParentUid;

            if (_bracer.IsHunterWithBracer(user, out var bracer))
            {
                var drain = mask.EnergyUsage * frameTime;

                if (bracer.Value.Comp.Charge < drain)
                {
                    mask.IsActive = false;
                    Dirty(uid, mask);
                    DisableVisionEffects(user);

                    if (mask.ToggleVisionAction != null)
                        _actions.SetToggled(mask.ToggleVisionAction.Value, false);

                    if (TryComp<ScopingComponent>(user, out var scoping) && scoping.Scope == uid)
                    {
                        _contentEye.ResetZoom(user);
                        RemComp<ScopingComponent>(user);
                    }

                    _popup.PopupEntity(Loc.GetString("st-bracer-no-power"), user, user);
                }
                else
                {
                    bracer.Value.Comp.Charge -= drain;
                    Dirty(bracer.Value.Owner, bracer.Value.Comp);
                }
            }
            else
                RemoveMaskAbilities((uid, mask), user);
        }
    }
}
