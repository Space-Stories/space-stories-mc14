using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RMC14.StationRecords;
using Content.Server._RMC14.StationRecords.Systems;
using Content.Server.Access.Systems;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Destructible;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Power;
using Content.Shared.Access.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CriminalRecords;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.MilitaryPolice;

/// <summary>
/// Security records, JAS incident reports and Overwatch's insubordination reports share station record keys.
/// </summary>
public sealed partial class RMCPoliceSystem : EntitySystem
{
    private const int MaxTextLength = 4096;
    private const int MaxCommentLength = 500;
    private const int MaxComments = 256;

    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly CriminalRecordsSystem _criminal = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IdCardSystem _id = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCPowerSystem _power = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly RMCStationRecordsSystem _rmcRecords = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    private EntProtoId<RMCMarineLawCatalogueComponent> _lawCatalogue;

    public EntProtoId<RMCMarineLawCatalogueComponent> LawCatalogue => _lawCatalogue;

    public override void Initialize()
    {
        base.Initialize();
        _lawCatalogue = _config.GetCVar(RMCCVars.RMCMarineLawCatalogue);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnRecordCreated);
        SubscribeLocalEvent<RMCStationRecordRegisteredEvent>(OnPersonRegistered);
        SubscribeLocalEvent<RecordModifiedEvent>(OnRecordModified);
        SubscribeLocalEvent<RecordRemovedEvent>(OnRecordRemoved);
        SubscribeLocalEvent<InventoryComponent, DidEquipEvent>(OnEquipped);
        SubscribeLocalEvent<InventoryComponent, DidUnequipEvent>(OnUnequipped);
        SubscribeLocalEvent<IdentityComponent, IdentityChangedEvent>(OnIdentityChanged);
        SubscribeLocalEvent<RMCPoliceUiComponent, ComponentGetStateAttemptEvent>(OnUiGetStateAttempt);
        SubscribeLocalEvent<RMCPoliceConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<RMCPoliceConsoleComponent, BoundUserInterfaceMessageAttempt>(OnMessageAttempt);
        SubscribeLocalEvent<RMCPoliceConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RMCPoliceConsoleComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<RMCPoliceConsoleComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<RMCPoliceConsoleComponent, BreakageEventArgs>(OnBreakage);
        SubscribeLocalEvent<RMCPoliceConsoleComponent, DamageChangedEvent>(OnDamageChanged,
            after: [typeof(DestructibleSystem)]);

        Subs.BuiEvents<RMCPoliceConsoleComponent>(RMCPoliceUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<RMCPoliceRecordMsg>(OnRecordMessage);
            subs.Event<RMCSentencingMsg>(OnSentencingMessage);
        });
        Subs.BuiEvents<OverwatchConsoleComponent>(OverwatchConsoleUI.Key, subs =>
            subs.Event<RMCOverwatchInsubordinationMsg>(OnInsubordination));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _lawCatalogue = _config.GetCVar(RMCCVars.RMCMarineLawCatalogue);
    }

    private void OnRecordCreated(AfterGeneralRecordCreatedEvent args)
    {
        // Store only the appearance needed by the mugshot, not the player's job or antagonist preferences.
        var portrait = args.Profile == null ? null : new HumanoidCharacterProfile
        {
            Name = args.Profile.Name,
            Age = args.Profile.Age,
            Species = args.Profile.Species,
            Appearance = args.Profile.Appearance,
        }.WithSex(args.Profile.Sex).WithGender(args.Profile.Gender);
        if (!_records.TryGetRecord<RMCPoliceRecord>(args.Key, out _))
            _records.AddRecordEntry(args.Key, new RMCPoliceRecord { Portrait = portrait });
        _records.Synchronize(args.Key);
    }

    private void OnPersonRegistered(RMCStationRecordRegisteredEvent args)
    {
        if (!_records.TryGetRecord<RMCPoliceRecord>(args.Key, out var security))
        {
            security = new RMCPoliceRecord();
            _records.AddRecordEntry(args.Key, security);
        }
        security.Portrait ??= args.Profile;
        if (!_records.TryGetRecord<CriminalRecord>(args.Key, out _))
            _records.AddRecordEntry(args.Key, new CriminalRecord { Status = ToSecurityStatus(security.Status) });
        RefreshHud(args.Person);
    }

    private void OnRecordModified(RecordModifiedEvent args)
    {
        // Other security consoles and cartridges can update the underlying station record too.
        if (_records.TryGetRecord<RMCPoliceRecord>(args.Key, out var security) &&
            _records.TryGetRecord<CriminalRecord>(args.Key, out var criminal) &&
            ToSecurityStatus(security.Status) != criminal.Status)
        {
            security.Status = criminal.Status switch
            {
                SecurityStatus.Wanted => RMCPoliceStatus.Arrest,
                SecurityStatus.Detained => RMCPoliceStatus.Incarcerated,
                SecurityStatus.Discharged => RMCPoliceStatus.Released,
                SecurityStatus.Suspected => RMCPoliceStatus.Suspect,
                _ => RMCPoliceStatus.None,
            };
        }
        RefreshStation(args.Key.OriginStation);
        RefreshHud();
    }

    private void OnRecordRemoved(RecordRemovedEvent args)
    {
        RefreshStation(args.Key.OriginStation);
        RefreshHud();
    }

    private bool CanUse(EntityUid console, EntityUid user)
    {
        var canGhostInteract = TryComp<GhostComponent>(user, out var ghost) && ghost.CanGhostInteract;
        return !(CompOrNull<DestructibleComponent>(console)?.IsBroken ?? false) &&
               _access.IsAllowed(user, console) && _power.IsPowered(console) &&
               (canGhostInteract || _mobState.IsAlive(user)) && _actionBlocker.CanInteract(user, console) &&
               _interaction.InRangeUnobstructed(user, console);
    }

    private void OnBreakage(Entity<RMCPoliceConsoleComponent> ent, ref BreakageEventArgs args)
    {
        _appearance.SetData(ent, RMCPoliceConsoleVisuals.Broken, true);
        _ui.CloseUis(ent.Owner);
    }

    private void OnDamageChanged(Entity<RMCPoliceConsoleComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased || CompOrNull<DestructibleComponent>(ent)?.IsBroken != false)
            return;
        _appearance.SetData(ent, RMCPoliceConsoleVisuals.Broken, false);
    }

    private void OnOpenAttempt(Entity<RMCPoliceConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanUse(ent, args.User))
            args.Cancel();
    }

    private void OnMessageAttempt(Entity<RMCPoliceConsoleComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (CanUse(ent, args.Actor))
            return;

        args.Cancel();
        Error(args.Actor, "rmc-police-access-denied");
    }

    private void OnOpened(Entity<RMCPoliceConsoleComponent> ent, ref BoundUIOpenedEvent args) => Refresh(ent);

    private void OnUiGetStateAttempt(Entity<RMCPoliceUiComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        // The database must not be sent to nearby players who are not using the console.
        if (args.Player?.AttachedEntity is not { } actor || !_ui.IsUiOpen(ent.Owner, RMCPoliceUiKey.Key, actor) ||
            !CanUse(ent.Owner, actor))
            args.Cancelled = true;
    }

    private void OnContainerInserted(Entity<RMCPoliceConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
        => Refresh(ent);

    private void OnContainerRemoved(Entity<RMCPoliceConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
        => Refresh(ent);

    private EntityUid? GetStation(EntityUid console)
    {
        var station = _station.GetOwningStation(console);
        return HasComp<StationRecordsComponent>(station) ? station : null;
    }

    private void RefreshStation(EntityUid station)
    {
        var query = EntityQueryEnumerator<RMCPoliceConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (GetStation(uid) == station && _ui.IsUiOpen(uid, RMCPoliceUiKey.Key))
                Refresh((uid, console));
        }
    }

    private void Refresh(Entity<RMCPoliceConsoleComponent> ent)
    {
        var station = GetStation(ent);
        var views = new List<RMCPoliceRecordView>();
        if (station != null)
        {
            foreach (var (id, general) in _records.GetRecordsOfType<GeneralStationRecord>(station.Value))
            {
                _records.TryGetRecord<RMCPoliceRecord>(new StationRecordKey(id, station.Value), out var security);
                views.Add(new RMCPoliceRecordView(id, general, security));
            }
        }

        var fingerprints = new List<RMCIncidentEntry>();
        var scannerConnected = TryScanner(ent, out var scanner);
        if (scannerConnected && scanner != null)
        {
            fingerprints = ClueEntries(scanner, views);
        }

        var data = EnsureComp<RMCPoliceUiComponent>(ent);
        data.Data = new RMCPoliceUiData(station != null, views, ent.Comp.Report, ent.Comp.Revision,
            scannerConnected, fingerprints, _lawCatalogue);
        Dirty(ent.Owner, data);
    }

    public void RefreshScanner(EntityUid scanner)
    {
        if (_containers.TryGetContainingContainer(scanner, out var container) &&
            TryComp<RMCPoliceConsoleComponent>(container.Owner, out var console))
            Refresh((container.Owner, console));
    }

    private List<RMCIncidentEntry> ClueEntries(RMCClueScannerComponent scanner,
        List<RMCPoliceRecordView> records)
    {
        var entries = new List<RMCIncidentEntry>();
        foreach (var clue in scanner.Clues)
        {
            var match = records.FirstOrDefault(view => !string.IsNullOrEmpty(clue.Fingerprint) &&
                                                       view.General.Fingerprint == clue.Fingerprint);
            var captured = !string.IsNullOrEmpty(clue.Name);
            var rank = captured ? clue.Rank : match?.General.JobTitle;
            var squad = captured ? clue.Squad : match?.General.Squad;
            entries.Add(new RMCIncidentEntry(
                captured ? clue.Name : match?.General.Name ?? Loc.GetString("rmc-police-unknown"),
                Loc.GetString("rmc-police-fingerprint-details",
                    ("rank", string.IsNullOrWhiteSpace(rank) ? Loc.GetString("rmc-police-unknown") : rank),
                    ("squad", string.IsNullOrWhiteSpace(squad) ? Loc.GetString("rmc-police-unknown") : squad),
                    ("fingerprint", clue.Fingerprint), ("description", clue.Description))));
        }
        return entries;
    }

    private bool TryScanner(EntityUid console, [NotNullWhen(true)] out RMCClueScannerComponent? scanner)
    {
        scanner = null;
        return _containers.TryGetContainer(console, RMCPoliceConsoleComponent.ScannerSlot, out var container) &&
               container.ContainedEntities.Count == 1 && TryComp(container.ContainedEntities[0], out scanner);
    }

    private string Officer(EntityUid actor)
    {
        if (TryWornId(actor, out var card))
            return $"{card.Comp.FullName ?? Identity.Name(actor, EntityManager)} ({card.Comp.LocalizedJobTitle})";
        return Identity.Name(actor, EntityManager);
    }

    private bool TryWornId(EntityUid wearer, out Entity<IdCardComponent> card)
    {
        card = default;
        return _inventory.TryGetSlotEntity(wearer, "id", out var worn) &&
               _id.TryGetIdCard(worn.Value, out card);
    }

    private void Error(EntityUid actor, LocId message, params (string, object)[] args)
    {
        _popup.PopupEntity(Loc.GetString(message, args), actor, actor, PopupType.MediumCaution);
    }

    private void LogChange(EntityUid actor, EntityUid console, string change)
    {
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} used {ToPrettyString(console)}: {change}");
    }

    private bool HighCommand(EntityUid actor) => _access.FindAccessTags(actor).Contains("CMAccessHighCommand");

    private void ScannerAction(Entity<RMCPoliceConsoleComponent> ent, RMCPoliceRecordMsg args)
    {
        if (!TryScanner(ent, out var scanner))
            return;

        switch (args.Action)
        {
            case RMCPoliceRecordAction.EjectScanner:
                _slots.TryEject(ent, RMCPoliceConsoleComponent.ScannerSlot, args.Actor, out _);
                break;
            case RMCPoliceRecordAction.ClearFingerprints:
                if (_containers.TryGetContainer(ent, RMCPoliceConsoleComponent.ScannerSlot, out var slot))
                    EntityManager.System<RMCPoliceClueSystem>().Clear((slot.ContainedEntities[0], scanner));
                break;
            case RMCPoliceRecordAction.PrintFingerprints:
                if (scanner.Clues.Count == 0)
                    break;
                var station = GetStation(ent);
                var records = new List<RMCPoliceRecordView>();
                if (station != null)
                {
                    foreach (var (id, general) in _records.GetRecordsOfType<GeneralStationRecord>(station.Value))
                        records.Add(new RMCPoliceRecordView(id, general, null));
                }
                Print(ent, args.Actor, ent.Comp.RecordPaper, Loc.GetString("rmc-police-fingerprints"),
                    FormatFingerprints(ClueEntries(scanner, records)));
                break;
        }

        Refresh(ent);
    }

    private bool TryHeldItem(EntityUid actor, [NotNullWhen(true)] out EntityUid? item)
    {
        // Resolve the other hand of a two-handed item, but reject placeholders for pulling or carrying.
        return _interaction.TryGetUsedEntity(actor, out item) && !TerminatingOrDeleted(item.Value) &&
               !HasComp<VirtualItemComponent>(item.Value) && HasComp<ItemComponent>(item.Value) &&
               _hands.IsHolding(actor, item);
    }

    private bool TryHeldId(EntityUid actor, [NotNullWhen(true)] out IdCardComponent? card,
        out StationRecordKey? key)
    {
        card = null;
        key = null;
        if (!TryHeldItem(actor, out var held) || !TryComp(held, out card))
            return false;

        key = CompOrNull<StationRecordKeyStorageComponent>(held.Value)?.Key;
        return !string.IsNullOrWhiteSpace(card.FullName);
    }

    private bool TrySuspectId(EntityUid actor, [NotNullWhen(true)] out IdCardComponent? card,
        out StationRecordKey? key)
    {
        if (TryHeldId(actor, out card, out key))
            return true;
        if (CompOrNull<PullerComponent>(actor)?.Pulling is not { } pulled ||
            !_interaction.InRangeUnobstructed(actor, pulled) || !TryWornId(pulled, out var id))
            return false;
        card = id.Comp;
        key = CompOrNull<StationRecordKeyStorageComponent>(id)?.Key;
        return !string.IsNullOrWhiteSpace(card.FullName);
    }

}
