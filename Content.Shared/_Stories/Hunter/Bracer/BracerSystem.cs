using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Input;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Stealth;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Hunter.Vision;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem : EntitySystem
{
    public const string PlasmaCasterSlotId = "st-bracer-plasma-caster-slot";
    public const string LeftAttachmentSlotId = "st-bracer-left-attachment-slot";
    public const string RightAttachmentSlotId = "st-bracer-right-attachment-slot";
    public const string AttachmentWeaponSlotId = "st-bracer-attachment-weapon-slot";
    public const string CasterHolderWeaponSlotId = "caster-gun-slot";
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly HunterVisionSystem _hunterVision = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializePower();
        InitializeSelfDestruct();
        InitializePlasmaCaster();
        InitializeCrafting();
        InitializeVerbs();
        InitializeAttachments();
        InitializeCloak();
        InitializeIdentity();

        SubscribeLocalEvent<HunterBracerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HunterBracerComponent, ToggleTranslatorEvent>(OnToggleTranslator);

        SubscribeLocalEvent<HunterBracerComponent, GotEquippedEvent>(OnBracerEquipped);
        SubscribeLocalEvent<HunterBracerComponent, GotUnequippedEvent>(OnBracerUnequipped);

        SubscribeLocalEvent<HunterBracerComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<HunterBracerComponent, EntInsertedIntoContainerMessage>(OnCasterOrAttachmentInserted);
        SubscribeLocalEvent<BracerAttachmentComponent, EntInsertedIntoContainerMessage>(OnAttachmentWeaponInserted);
        SubscribeLocalEvent<HunterBracerComponent, EntRemovedFromContainerMessage>(OnAttachmentRemoved);
        SubscribeLocalEvent<HunterBracerComponent, ExaminedEvent>(OnBracerExamine);
        SubscribeLocalEvent<HunterBracerComponent, InteractUsingEvent>(
            OnBracerInteractUsing,
            new[] { typeof(ItemSlotsSystem) }
        );
        SubscribeLocalEvent<HunterBracerComponent, InstallAttachmentDialogEvent>(OnInstallDialogSelected);

        CommandBinds
            .Builder.Bind(
                CMKeyFunctions.CMUniqueAction,
                InputCmdHandler.FromDelegate(HandleCasterUniqueAction, handle: false)
            )
            .Register<BracerSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdatePower(frameTime);
        UpdateSelfDestruct();
        UpdateCloak();
    }

    public bool IsHunterWithBracer(EntityUid uid, [NotNullWhen(true)] out Entity<HunterBracerComponent>? bracer)
    {
        bracer = null;
        if (!HasComp<HunterComponent>(uid))
            return false;

        if (
            !_inventory.TryGetSlotEntity(uid, "gloves", out var equipped)
            || !TryComp(equipped, out HunterBracerComponent? bracerComp)
        )
            return false;

        bracer = (equipped.Value, bracerComp);
        return true;
    }

    private void OnBracerEquipped(Entity<HunterBracerComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot == "gloves")
        {
            ent.Comp.Locked = true;
            Dirty(ent);
        }

        OnBracerEquippedPower(ent, ref args);
        OnEquippedIdentity(ent, ref args);

        if (_net.IsServer)
            _hunterVision.HandleBracerEquipped(args.Equipee);
    }

    private void OnBracerUnequipped(Entity<HunterBracerComponent> ent, ref GotUnequippedEvent args)
    {
        if (_net.IsServer)
        {
            RetractAttachments(args.Equipee, ent, true);
            RetractPlasmaCaster(args.Equipee, ent);

            if (HasComp<BracerCloakedComponent>(args.Equipee))
                SetCloak(args.Equipee, ent, false, true, false);

            RemComp<ActiveHunterBracerComponent>(ent);
            _alerts.ClearAlert(args.Equipee, ent.Comp.BracerPowerAlert);
            RemComp<EntityTurnInvisibleComponent>(args.Equipee);

            _hunterVision.HandleBracerUnequipped(args.Equipee);
        }

        OnUnequippedIdentity(ent, ref args);
    }

    private void OnBracerInteractUsing(Entity<HunterBracerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<BracerAttachmentComponent>(args.Used, out _))
        {
            args.Handled = true;
            if (_net.IsClient)
                return;

            var userNet = GetNetEntity(args.User);
            var options = new List<DialogOption>
            {
                new(
                    "Left Slot",
                    new InstallAttachmentDialogEvent(GetNetEntity(ent), GetNetEntity(args.Used), userNet, "Left")
                ),
                new(
                    "Right Slot",
                    new InstallAttachmentDialogEvent(GetNetEntity(ent), GetNetEntity(args.Used), userNet, "Right")
                ),
            };

            _dialog.OpenOptions(ent.Owner, args.User, Loc.GetString("st-bracer-install-attachment-title"), options);
            return;
        }

        if (HasComp<ItemSlotsComponent>(args.Used) && _itemSlots.TryGetSlot(args.Used, CasterHolderWeaponSlotId, out _))
        {
            args.Handled = true;
            if (ent.Comp.CasterDeployed)
            {
                _popup.PopupClient(Loc.GetString("st-bracer-caster-already-deployed"), args.User, args.User);
                return;
            }

            if (_itemSlots.TryGetSlot(ent, PlasmaCasterSlotId, out var slot))
            {
                if (!_itemSlots.TryInsert(ent, slot, args.Used, args.User))
                    _popup.PopupClient(Loc.GetString("st-bracer-caster-insert-fail"), args.User, args.User);
            }
        }
    }

    private void OnInstallDialogSelected(Entity<HunterBracerComponent> bracer, ref InstallAttachmentDialogEvent args)
    {
        var user = GetEntity(args.User);
        if (!user.IsValid())
            return;

        var attachmentUid = GetEntity(args.Attachment);

        if (!Exists(attachmentUid) || !_hands.IsHolding(user, attachmentUid))
            return;

        var slotId = args.Slot == "Left" ? LeftAttachmentSlotId : RightAttachmentSlotId;

        if (_itemSlots.TryGetSlot(bracer.Owner, slotId, out var slot))
        {
            if (_itemSlots.TryInsert(bracer.Owner, slot, attachmentUid, user))
            {
                _popup.PopupEntity(
                    Loc.GetString("st-bracer-install-success", ("item", Name(attachmentUid))),
                    user,
                    user
                );
            }
            else
                _popup.PopupEntity(Loc.GetString("st-bracer-install-fail"), user, user);
        }
    }

    private void OnCasterOrAttachmentInserted(
        Entity<HunterBracerComponent> ent,
        ref EntInsertedIntoContainerMessage args
    )
    {
        if (args.Container.ID == PlasmaCasterSlotId)
        {
            if (
                _itemSlots.TryGetSlot(args.Entity, CasterHolderWeaponSlotId, out var holderSlot)
                && holderSlot.Item is { } weaponUid
            )
            {
                if (TryComp<PlasmaCasterComponent>(weaponUid, out var caster))
                {
                    caster.Bracer = GetNetEntity(ent.Owner);
                    Dirty(weaponUid, caster);
                }
            }
        }
        else if (args.Container.ID == LeftAttachmentSlotId || args.Container.ID == RightAttachmentSlotId)
        {
            var wearer = Transform(ent).ParentUid;
            if (wearer.IsValid())
                UpdateAttachmentActionState(ent, wearer);
        }
    }

    private void OnAttachmentWeaponInserted(
        Entity<BracerAttachmentComponent> ent,
        ref EntInsertedIntoContainerMessage args
    )
    {
        if (args.Container.ID != AttachmentWeaponSlotId)
            return;

        if (
            !_container.TryGetContainingContainer(ent.Owner, out var container)
            || !TryComp<HunterBracerComponent>(container.Owner, out _)
        )
            return;

        if (TryComp<BracerAttachedWeaponComponent>(args.Entity, out var weaponComp))
        {
            weaponComp.Bracer = GetNetEntity(container.Owner);
            Dirty(args.Entity, weaponComp);
        }
    }

    private void OnAttachmentRemoved(Entity<HunterBracerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != LeftAttachmentSlotId && args.Container.ID != RightAttachmentSlotId)
            return;

        var wearer = Transform(ent).ParentUid;
        if (wearer.IsValid())
            UpdateAttachmentActionState(ent, wearer);
    }

    private void OnUnequipAttempt(Entity<HunterBracerComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (HasComp<BracerSelfDestructingComponent>(ent))
        {
            _popup.PopupClient(Loc.GetString("st-bracer-sd-cannot-unequip"), args.Unequipee, args.Unequipee);
            args.Cancel();
            return;
        }

        if (ent.Comp.Locked)
            args.Cancel();
    }

    private void OnBracerExamine(Entity<HunterBracerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var charge = (int)ent.Comp.Charge;
        var maxCharge = (int)ent.Comp.MaxCharge;

        args.PushMarkup(Loc.GetString("st-bracer-examine-charge", ("charge", charge), ("maxCharge", maxCharge)));
    }

    [Serializable] [NetSerializable]
    public sealed class InstallAttachmentDialogEvent : EntityEventArgs
    {
        public NetEntity Attachment;
        public NetEntity Bracer;
        public string Slot;
        public NetEntity User;

        public InstallAttachmentDialogEvent(NetEntity bracer, NetEntity attachment, NetEntity user, string slot)
        {
            Bracer = bracer;
            Attachment = attachment;
            User = user;
            Slot = slot;
        }
    }
}
