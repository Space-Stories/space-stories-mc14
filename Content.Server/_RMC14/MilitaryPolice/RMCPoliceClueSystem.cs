using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Destructible;
using Content.Server.Forensics;
using Content.Server.Wires;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Wires;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.MilitaryPolice;

/// <summary>
/// CM's incident clues and accumulating MP scanner. Ordinary SS14 item fingerprints can also be collected.
/// </summary>
public sealed class RMCPoliceClueSystem : EntitySystem
{
    private static readonly EntProtoId<RMCPoliceClueComponent> CluePrototype = "RMCPoliceClue";
    private static readonly ProtoId<ReagentPrototype> ForensicSpray = "RMCForensicSpray";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IdCardSystem _id = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly RMCPoliceSystem _police = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private readonly HashSet<Entity<RMCPoliceClueComponent>> _nearbyClues = new();
    private readonly HashSet<EntityUid> _tileEntities = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RMCClueScannerComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<RMCClueScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<RMCClueScannerComponent, RMCPoliceScanDoAfterEvent>(OnScanFinished);
        SubscribeLocalEvent<RMCClueScannerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RMCClueScannerComponent, EntityUnpausedEvent>(OnScannerUnpaused);
        SubscribeLocalEvent<RMCPoliceClueComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<RMCPoliceClueComponent, EntityUnpausedEvent>(OnClueUnpaused);
        SubscribeLocalEvent<RMCPoliceClueOnDestructionComponent, DamageChangedEvent>(OnDamage,
            before: new[] { typeof(DestructibleSystem) });
        SubscribeLocalEvent<RMCPoliceClueOnDestructionComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<RMCPoliceClueOnDestructionComponent, DestructionEventArgs>(OnDestroy);
        SubscribeLocalEvent<CMAutomatedVendorComponent, RMCAutomatedVendorHackedEvent>(OnVendorHacked);
        SubscribeLocalEvent<WiresComponent, WireCutEvent>(OnWireCut);
        SubscribeLocalEvent<WiresComponent, WireMendedEvent>(OnWireMended);
        SubscribeLocalEvent<RMCApcComponent, RMCApcMainPowerWireCutEvent>(OnApcWireCut);
        SubscribeLocalEvent<RMCApcComponent, RMCApcMainPowerWirePulsedEvent>(OnApcWirePulsed);
        SubscribeLocalEvent<RMCApcComponent, RMCApcCellRemovedEvent>(OnApcCellRemoved);
        SubscribeLocalEvent<RMCApcComponent, RMCApcElectronicsRemovedEvent>(OnApcElectronicsRemoved);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _nextUpdate = TimeSpan.Zero;
        _nearbyClues.Clear();
        _tileEntities.Clear();
    }

    private void OnUse(Entity<RMCClueScannerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = StartScan(ent, args.User);
    }

    private void OnAfterInteract(Entity<RMCClueScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !HasComp<RMCPoliceClueComponent>(target) && !HasComp<ForensicsComponent>(target))
            return;
        args.Handled = StartScan(ent, args.User, target);
    }

    private bool StartScan(Entity<RMCClueScannerComponent> ent, EntityUid user, EntityUid? target = null)
    {
        if (ent.Comp.Scanning || !_hands.IsHolding(user, ent) ||
            target is { } entity && HasComp<VirtualItemComponent>(entity))
            return false;
        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.RequiredSkill))
        {
            _popup.PopupEntity(Loc.GetString("rmc-police-scanner-unskilled"), ent, user);
            return false;
        }
        if (ent.Comp.Clues.Count >= ent.Comp.MaxClues)
        {
            _popup.PopupEntity(Loc.GetString("rmc-police-scanner-full"), ent, user);
            return false;
        }
        var args = new DoAfterArgs(EntityManager, user, ent.Comp.ScanDuration,
            new RMCPoliceScanDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (!_doAfter.TryStartDoAfter(args))
            return false;
        ent.Comp.Scanning = true;
        UpdateAppearance(ent);
        _popup.PopupEntity(Loc.GetString("rmc-police-scanner-start"), ent, user);
        return true;
    }

    private void OnScanFinished(Entity<RMCClueScannerComponent> ent, ref RMCPoliceScanDoAfterEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        ent.Comp.Scanning = false;
        if (args.Cancelled || !_hands.IsHolding(args.User, ent) ||
            !_skills.HasSkill(args.User, ent.Comp.Skill, ent.Comp.RequiredSkill))
        {
            UpdateAppearance(ent);
            return;
        }

        var found = 0;
        if (args.Target is { } target)
        {
            if (!Deleted(target) && _interaction.InRangeUnobstructed(args.User, target))
                found = CollectTarget(ent, target);
        }
        else
        {
            // BYOND's range(2) is a square, including the diagonal tiles.
            var coordinates = _transform.GetMapCoordinates(args.User);
            _nearbyClues.Clear();
            _lookup.GetEntitiesInRange(coordinates, ent.Comp.Range * MathF.Sqrt(2) + 0.01f, _nearbyClues);
            foreach (var clue in _nearbyClues)
            {
                var position = _transform.GetMapCoordinates(clue);
                var delta = position.Position - coordinates.Position;
                if (position.MapId == coordinates.MapId && MathF.Abs(delta.X) <= ent.Comp.Range &&
                    MathF.Abs(delta.Y) <= ent.Comp.Range)
                    found += CollectTarget(ent, clue);
            }
        }
        UpdateAppearance(ent);
        _popup.PopupEntity(Loc.GetString(found == 0 ? "rmc-police-scanner-empty" : "rmc-police-scanner-found",
            ("found", found), ("total", ent.Comp.Clues.Count)), ent, args.User);
    }

    private int CollectTarget(Entity<RMCClueScannerComponent> scanner, EntityUid target)
    {
        if (EntityManager.IsQueuedForDeletion(target) || HasComp<VirtualItemComponent>(target) ||
            scanner.Comp.Clues.Count >= scanner.Comp.MaxClues)
            return 0;
        if (TryComp<RMCPoliceClueComponent>(target, out var clue))
        {
            if (clue.Clue.ExpiresAt <= _timing.CurTime)
                return 0;
            scanner.Comp.Clues.Add(clue.Clue);
            QueueDel(target);
            return 1;
        }
        if (!TryComp<ForensicsComponent>(target, out var forensics))
            return 0;

        var source = GetNetEntity(target);
        var found = 0;
        foreach (var fingerprint in forensics.Fingerprints.Order())
        {
            if (scanner.Comp.Clues.Count >= scanner.Comp.MaxClues)
                break;
            if (scanner.Comp.Clues.Any(stored => stored.Source == source && stored.Fingerprint == fingerprint))
                continue;
            scanner.Comp.Clues.Add(new RMCPoliceClue
            {
                Fingerprint = fingerprint,
                Source = source,
                Description = Loc.GetString("rmc-police-clue-item", ("item", Name(target))),
            });
            found++;
        }
        return found;
    }

    private void OnExamine(Entity<RMCClueScannerComponent> ent, ref ExaminedEvent args)
        => args.PushMarkup(Loc.GetString("rmc-police-scanner-stored", ("count", ent.Comp.Clues.Count)));

    public void Clear(Entity<RMCClueScannerComponent> ent)
    {
        ent.Comp.Clues.Clear();
        UpdateAppearance(ent);
    }

    private void OnClueUnpaused(Entity<RMCPoliceClueComponent> ent, ref EntityUnpausedEvent args)
    {
        if (ent.Comp.RevealedUntil != TimeSpan.Zero)
            ent.Comp.RevealedUntil += args.PausedTime;
        if (ent.Comp.Clue.ExpiresAt != null)
            ent.Comp.Clue.ExpiresAt += args.PausedTime;
    }

    private void OnScannerUnpaused(Entity<RMCClueScannerComponent> ent, ref EntityUnpausedEvent args)
    {
        foreach (var clue in ent.Comp.Clues)
        {
            if (clue.ExpiresAt != null)
                clue.ExpiresAt += args.PausedTime;
        }
    }

    private void UpdateAppearance(Entity<RMCClueScannerComponent> ent)
    {
        _appearance.SetData(ent, RMCPoliceClueVisuals.Scanning, ent.Comp.Scanning);
        _appearance.SetData(ent, RMCPoliceClueVisuals.Stored, ent.Comp.Clues.Count > 0);
    }

    /// <summary>
    /// Capture the incident before subsequent ID changes, record edits or destruction of its source.
    /// </summary>
    public EntityUid? CreateClue(EntityUid user, LocId description)
    {
        if (!HasComp<HumanoidAppearanceComponent>(user) || Deleted(user) ||
            Transform(user).MapID == MapId.Nullspace)
            return null;

        var clue = new RMCPoliceClue
        {
            Name = Name(user),
            Fingerprint = CompOrNull<FingerprintComponent>(user)?.Fingerprint ?? string.Empty,
            Description = Loc.GetString(description),
        };
        if (_inventory.TryGetSlotEntity(user, "id", out var worn) && _id.TryGetIdCard(worn.Value, out var card))
            clue.Rank = card.Comp.LocalizedJobTitle ?? string.Empty;
        if (CompOrNull<SquadMemberComponent>(user)?.Squad is { } squad)
            clue.Squad = Name(squad);

        var uid = Spawn(CluePrototype, Transform(user).Coordinates);
        var component = Comp<RMCPoliceClueComponent>(uid);
        clue.ExpiresAt = _timing.CurTime + component.Lifetime;
        component.Clue = clue;
        return uid;
    }

    private void OnDamage(Entity<RMCPoliceClueOnDestructionComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            ent.Comp.DamageOrigin = args.Origin;
    }

    private void OnBreak(Entity<RMCPoliceClueOnDestructionComponent> ent, ref BreakageEventArgs args)
        => LeaveDestructionClue(ent);

    private void OnDestroy(Entity<RMCPoliceClueOnDestructionComponent> ent, ref DestructionEventArgs args)
        => LeaveDestructionClue(ent);

    private void LeaveDestructionClue(Entity<RMCPoliceClueOnDestructionComponent> ent)
    {
        if (ent.Comp.Created || ent.Comp.DamageOrigin is not { } user ||
            ent.Comp.MainshipOnly && !IsOnMainship(ent))
            return;
        ent.Comp.Created = CreateClue(user, ent.Comp.Description) != null;
    }

    private bool IsOnMainship(EntityUid uid)
    {
        var transform = Transform(uid);
        return HasComp<AlmayerComponent>(transform.MapUid) || HasComp<AlmayerComponent>(transform.GridUid);
    }

    private void OnVendorHacked(Entity<CMAutomatedVendorComponent> ent, ref RMCAutomatedVendorHackedEvent args)
    {
        if (IsOnMainship(ent))
            CreateClue(args.User, "rmc-police-clue-vendor");
    }

    private void OnWireCut(Entity<WiresComponent> ent, ref WireCutEvent args)
        => LeaveAirlockWireClue(ent, args.User);

    private void OnWireMended(Entity<WiresComponent> ent, ref WireMendedEvent args)
        => LeaveAirlockWireClue(ent, args.User);

    private void LeaveAirlockWireClue(EntityUid uid, EntityUid user)
    {
        if (!HasComp<AirlockComponent>(uid) || !IsOnMainship(uid) ||
            HasComp<RMCPoliceAirlockClueCreatedComponent>(uid))
            return;
        if (CreateClue(user, "rmc-police-clue-airlock-wire") != null)
            AddComp<RMCPoliceAirlockClueCreatedComponent>(uid);
    }

    private void OnApcWireCut(Entity<RMCApcComponent> ent, ref RMCApcMainPowerWireCutEvent args)
        => CreateClue(args.User, "rmc-police-clue-apc-wire");

    private void OnApcWirePulsed(Entity<RMCApcComponent> ent, ref RMCApcMainPowerWirePulsedEvent args)
        => CreateClue(args.User, "rmc-police-clue-apc-multitool");

    private void OnApcCellRemoved(Entity<RMCApcComponent> ent, ref RMCApcCellRemovedEvent args)
        => CreateClue(args.User, "rmc-police-clue-apc-cell");

    private void OnApcElectronicsRemoved(Entity<RMCApcComponent> ent, ref RMCApcElectronicsRemovedEvent args)
        => CreateClue(args.User, "rmc-police-clue-apc-electronics");

    private void OnReaction(Entity<RMCPoliceClueComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method == ReactionMethod.Touch && args.Reagent.ID == ForensicSpray &&
            args.ReagentQuantity.Quantity > 0)
            Reveal(ent);
    }

    public void Reveal(Entity<RMCPoliceClueComponent> ent)
    {
        ent.Comp.RevealedUntil = _timing.CurTime + ent.Comp.RevealDuration;
        SetRevealed(ent, true);
    }

    public void RevealTile(TileRef tile)
    {
        _tileEntities.Clear();
        // Keep the full tile bounds so prints at its edge are included; filter neighboring tiles below.
        _lookup.GetEntitiesIntersecting(tile.GridUid, _lookup.GetWorldBounds(tile), _tileEntities, LookupFlags.All);
        foreach (var uid in _tileEntities)
        {
            if (TryComp<RMCPoliceClueComponent>(uid, out var clue) && Transform(uid).GridUid == tile.GridUid &&
                _transform.TryGetGridTilePosition(uid, out var indices) && indices == tile.GridIndices)
                Reveal((uid, clue));
        }
    }

    private void SetRevealed(EntityUid uid, bool revealed)
    {
        var visibility = Comp<RMCPoliceClueVisibilityComponent>(uid);
        if (visibility.Revealed == revealed)
            return;
        visibility.Revealed = revealed;
        Dirty(uid, visibility);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var clues = EntityQueryEnumerator<RMCPoliceClueComponent>();
        while (clues.MoveNext(out var uid, out var clue))
        {
            if (clue.Clue.ExpiresAt <= _timing.CurTime)
                QueueDel(uid);
            if (clue.RevealedUntil <= _timing.CurTime)
                SetRevealed(uid, false);
        }
        var scanners = EntityQueryEnumerator<RMCClueScannerComponent>();
        while (scanners.MoveNext(out var uid, out var scanner))
        {
            if (scanner.Clues.RemoveAll(clue => clue.ExpiresAt <= _timing.CurTime) == 0)
                continue;
            UpdateAppearance((uid, scanner));
            _police.RefreshScanner(uid);
        }
    }
}
