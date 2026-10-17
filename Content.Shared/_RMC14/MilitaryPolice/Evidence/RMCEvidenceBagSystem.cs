using Content.Shared._RMC14.CombatMode;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.MilitaryPolice.Evidence;

public sealed class RMCEvidenceBagSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCEvidenceBagComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RMCEvidenceBagComponent, BeforeRangedInteractEvent>(OnBeforeRangedInteract);
        SubscribeLocalEvent<RMCEvidenceBagComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<RMCEvidenceBagComponent, CanDropDraggedEvent>(OnCanDropDragged);
        SubscribeLocalEvent<RMCEvidenceBagComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<RMCEvidenceBagComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RMCEvidenceBagComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<RMCEvidenceBagComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<RMCEvidenceBagComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<RMCEvidenceBagComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<RMCEvidenceBagComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<RMCEvidenceBagComponent, GotReclaimedEvent>(OnReclaimed);
        SubscribeLocalEvent<RMCEvidenceBagComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<HandsComponent, RMCCombatModeInteractOverrideUserEvent>(OnCombatModeInteract);
    }

    private void OnInit(Entity<RMCEvidenceBagComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
    }

    private void OnBeforeRangedInteract(Entity<RMCEvidenceBagComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !TryComp<ItemComponent>(target, out var item))
        {
            return;
        }

        // This event contacts only the used bag, keeping the collector's fingerprints off the evidence.
        if (TryCollect(ent, args.User, target))
        {
            args.Handled = true;
            return;
        }

        if (HasComp<StorageComponent>(target))
            return;

        args.Handled = true;
        var message = ent.Comp.Container.ContainedEntity != null
            ? "rmc-evidence-bag-full"
            : _item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize)
                ? "rmc-evidence-bag-too-large"
                : "rmc-evidence-bag-cannot-collect";
        _popup.PopupClient(Loc.GetString(message), ent, args.User);
    }

    private void OnCombatModeInteract(Entity<HandsComponent> ent, ref RMCCombatModeInteractOverrideUserEvent args)
    {
        if (args.Handled || args.Target is not { } target || !HasComp<ItemComponent>(target) ||
            _hands.GetActiveItem((ent.Owner, ent.Comp)) is not { } held || !HasComp<RMCEvidenceBagComponent>(held))
        {
            return;
        }

        args.CanInteract = true;
        args.Handled = true;
    }

    private void OnCanDrag(Entity<RMCEvidenceBagComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnCanDropDragged(Entity<RMCEvidenceBagComponent> ent, ref CanDropDraggedEvent args)
    {
        args.Handled = true;
        args.CanDrop = CanCollect(ent, args.User, args.Target);
    }

    private void OnDragDropDragged(Entity<RMCEvidenceBagComponent> ent, ref DragDropDraggedEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryCollect(ent, args.User, args.Target);
    }

    private bool CanCollect(Entity<RMCEvidenceBagComponent> ent, EntityUid user, EntityUid target)
    {
        if (!_hands.IsHolding(user, ent) ||
            !_actionBlocker.CanInteract(user, ent) ||
            !_actionBlocker.CanUseHeldEntity(user, ent) ||
            !_actionBlocker.CanPickup(user, target) ||
            !_interaction.InRangeAndAccessible(user, target) ||
            !_container.CanInsert(target, ent.Comp.Container))
        {
            return false;
        }

        if (!_container.TryGetContainingContainer(target, out var source))
            return true;

        if (_hands.IsHolding(user, target))
            return _hands.CanDrop(user, target);

        if (source.ID != StorageComponent.ContainerId ||
            !TryComp<StorageComponent>(source.Owner, out var storage) ||
            !_storage.CanInteract(user, (source.Owner, storage)))
        {
            return false;
        }

        // Only collect from the user's own carried storage, including one nested storage item.
        var owner = source.Owner;
        for (var depth = 0; depth < 2; depth++)
        {
            if (!_container.TryGetContainingContainer(owner, out var parent))
                return false;

            if (parent.Owner == user)
                return true;

            if (parent.ID != StorageComponent.ContainerId)
                return false;

            owner = parent.Owner;
        }

        return false;
    }

    private bool TryCollect(Entity<RMCEvidenceBagComponent> ent, EntityUid user, EntityUid target)
    {
        if (!CanCollect(ent, user, target))
            return false;

        var inserted = _hands.IsHolding(user, target)
            ? _hands.TryDropIntoContainer(user, target, ent.Comp.Container)
            : _container.Insert(target, ent.Comp.Container);
        if (!inserted)
            return false;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} collected {ToPrettyString(target):entity} in {ToPrettyString(ent):entity}");
        _popup.PopupClient(Loc.GetString("rmc-evidence-bag-collected", ("item", target)), ent, user);
        return true;
    }

    private void OnInsertAttempt(Entity<RMCEvidenceBagComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container != ent.Comp.Container)
            return;

        if (HasComp<RMCEvidenceBagComponent>(args.EntityUid) ||
            HasComp<VirtualItemComponent>(args.EntityUid) ||
            Transform(args.EntityUid).Anchored ||
            !TryComp<ItemComponent>(args.EntityUid, out var item) ||
            _item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize))
        {
            args.Cancel();
        }
    }

    private void OnInserted(Entity<RMCEvidenceBagComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!_timing.ApplyingState && args.Container == ent.Comp.Container &&
            TryComp<ItemComponent>(args.Entity, out var item))
            _item.SetSize(ent, item.Size);
    }

    private void OnRemoved(Entity<RMCEvidenceBagComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!_timing.ApplyingState && args.Container == ent.Comp.Container)
            _item.SetSize(ent, ent.Comp.EmptySize);
    }

    private void OnUseInHand(Entity<RMCEvidenceBagComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryExtract(ent, args.User);
    }

    private void OnGetAlternativeVerbs(Entity<RMCEvidenceBagComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract ||
            !_hands.IsHolding(args.User, ent) || ent.Comp.Container.ContainedEntity == null)
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("rmc-evidence-bag-extract"),
            Category = VerbCategory.Eject,
            Act = () => TryExtract(ent, user),
        });
    }

    private bool TryExtract(Entity<RMCEvidenceBagComponent> ent, EntityUid user)
    {
        if (!_hands.IsHolding(user, ent) ||
            !_actionBlocker.CanInteract(user, ent) ||
            !_actionBlocker.CanUseHeldEntity(user, ent) ||
            ent.Comp.Container.ContainedEntity is not { } evidence ||
            !_actionBlocker.CanPickup(user, evidence) ||
            !_container.Remove(evidence, ent.Comp.Container))
        {
            return false;
        }

        _hands.PickupOrDrop(user, evidence, dropNear: true);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} removed {ToPrettyString(evidence):entity} from {ToPrettyString(ent):entity}");
        return true;
    }

    private void OnDestroyed(Entity<RMCEvidenceBagComponent> ent, ref DestructionEventArgs args)
    {
        _container.EmptyContainer(ent.Comp.Container, force: true);
    }

    private void OnReclaimed(Entity<RMCEvidenceBagComponent> ent, ref GotReclaimedEvent args)
    {
        _container.EmptyContainer(ent.Comp.Container, force: true, destination: args.ReclaimerCoordinates);
    }

    private void OnExamined(Entity<RMCEvidenceBagComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Container.ContainedEntity is not { } evidence)
        {
            args.PushMarkup(Loc.GetString("rmc-evidence-bag-examine-empty"));
            return;
        }

        args.PushMarkup(Loc.GetString("rmc-evidence-bag-examine-full", ("item", evidence)));
        args.PushMessage(_examine.GetExamineText(evidence, args.Examiner));
    }
}
