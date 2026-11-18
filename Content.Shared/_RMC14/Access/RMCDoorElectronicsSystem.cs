using System.Linq;
using Content.Shared._RMC14.UserInterface;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Electronics;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Access;

public sealed class RMCDoorElectronicsSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly RMCUserInterfaceSystem _rmcUi = default!;

    public static readonly ProtoId<AccessGroupPrototype> MarineAccessCatalog = "RMCDoorElectronicsAccess";

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCDoorElectronicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RMCDoorElectronicsComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RMCDoorElectronicsComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
        SubscribeLocalEvent<RMCAirlockRequireAllAccessComponent, OnAccessOverriderAccessUpdatedEvent>(OnAirlockAccessOverridden);
        Subs.BuiEvents<RMCDoorElectronicsComponent>(RMCDoorElectronicsUiKey.Key,
            subs =>
            {
                subs.Event<DoorElectronicsUpdateConfigurationMessage>(OnChangeConfiguration);
                subs.Event<RMCDoorElectronicsSetModeMessage>(OnSetMode);
            });
    }

    private void OnStartup(Entity<RMCDoorElectronicsComponent> ent, ref ComponentStartup args)
    {
        if (!_net.IsServer)
            return;

        var allowed = _prototypes.Index(ent.Comp.AccessCatalog).Tags;
        if (TryComp<AccessReaderComponent>(ent, out var legacyReader))
        {
            if (ent.Comp.SelectedAccesses.Count == 0)
            {
                ent.Comp.SelectedAccesses = legacyReader.AccessLists.SelectMany(group => group)
                    .Where(allowed.Contains).Distinct().ToList();
                if (legacyReader.AccessLists.Count > 1)
                    ent.Comp.RequireAll = false;

                Dirty(ent);
            }

            RemComp<AccessReaderComponent>(ent);
        }

        if (ent.Comp.SelectedAccesses.RemoveAll(access => !allowed.Contains(access)) > 0)
            Dirty(ent);
    }

    private void OnAirlockAccessOverridden(Entity<RMCAirlockRequireAllAccessComponent> ent, ref OnAccessOverriderAccessUpdatedEvent args)
    {
        if (TryComp<AccessReaderComponent>(ent, out var reader) && reader.AccessLists.Count > 1)
        {
            var combined = reader.AccessLists.SelectMany(group => group).ToHashSet();
            _accessReader.SetAccesses((ent, reader), new List<HashSet<ProtoId<AccessLevelPrototype>>> { combined });
        }
    }

    private void OnUiMessageAttempt(Entity<RMCDoorElectronicsComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        var editsSettings = args.Message is DoorElectronicsUpdateConfigurationMessage or RMCDoorElectronicsSetModeMessage;
        if (args.UiKey is not RMCDoorElectronicsUiKey.Key ||
            !_rmcUi.CanUseUI(args) || // Stories-EngineeringUiValidation (reuse the existing fork check)
            !_hands.IsHolding(args.Actor, ent) ||
            (ent.Comp.Locked && editsSettings))
        {
            args.Cancel();
        }
    }

    private void OnInteractUsing(Entity<RMCDoorElectronicsComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp(args.Used, out IdCardComponent? card) || TerminatingOrDeleted(args.Used) ||
            !_hands.IsHolding(args.User, args.Used) ||
            !_interaction.InRangeAndAccessible(args.User, ent.Owner) ||
            !_rmcUi.CanOpenUI((ent.Owner, null), (args.User, null), RMCDoorElectronicsUiKey.Key))
        {
            return;
        }

        args.Handled = true;
        if (!ent.Comp.EditorAccess.Overlaps(_accessReader.FindAccessTags(args.Used)))
        {
            _popup.PopupClient(Loc.GetString("rmc-access-denied"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.Locked)
            ent.Comp.LastOperator = card.FullName;

        ent.Comp.Locked = !ent.Comp.Locked;
        Dirty(ent);
        var result = ent.Comp.Locked ? "rmc-door-electronics-locked" : "rmc-door-electronics-unlocked";
        _popup.PopupClient(Loc.GetString(result), ent, args.User);
    }

    private void OnChangeConfiguration(Entity<RMCDoorElectronicsComponent> ent, ref DoorElectronicsUpdateConfigurationMessage args)
    {
        var allowed = _prototypes.Index(ent.Comp.AccessCatalog).Tags;
        if (args.AccessList is not { } accesses ||
            accesses.Count > allowed.Count ||
            !accesses.TrueForAll(allowed.Contains))
        {
            return;
        }

        var selected = accesses.Distinct().ToList();
        if (ent.Comp.SelectedAccesses.SequenceEqual(selected))
            return;

        ent.Comp.SelectedAccesses = selected;
        Dirty(ent);
    }

    private void OnSetMode(Entity<RMCDoorElectronicsComponent> ent, ref RMCDoorElectronicsSetModeMessage args)
    {
        if (ent.Comp.RequireAll == args.RequireAll)
            return;

        ent.Comp.RequireAll = args.RequireAll;
        Dirty(ent);
    }

    public void ApplyConfiguration(Entity<RMCDoorElectronicsComponent> board, Entity<AccessReaderComponent> door)
    {
        var allowed = _prototypes.Index(board.Comp.AccessCatalog).Tags;
        var selected = board.Comp.SelectedAccesses.Where(allowed.Contains).ToHashSet();
        if (board.Comp.RequireAll)
            EnsureComp<RMCAirlockRequireAllAccessComponent>(door);

        if (board.Comp.RequireAll && selected.Count > 0)
            _accessReader.SetAccesses(door, new List<HashSet<ProtoId<AccessLevelPrototype>>> { selected });
        else
            _accessReader.SetAccesses(door, selected.ToList());
    }
}
