using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._Stories.Ordnance.Assemblies;
using Content.Shared._Stories.Ordnance.Triggers;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Sticky;
using Content.Shared.Sticky.Components;
using Content.Shared.Sticky.Systems;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance;

[ByRefEvent]
public record struct OrdnanceCasingLockedEvent();

public sealed class SharedOrdnanceCasingSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly StepTriggerSystem _stepTrigger = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GunIFFSystem _gunIff = default!;
    [Dependency] private readonly StickySystem _sticky = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrdnanceCasingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrdnanceCasingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OrdnanceCasingComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<OrdnanceCasingComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<OrdnanceCasingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<OrdnanceCasingComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<OrdnanceCasingComponent, EntInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<OrdnanceCasingComponent, EntRemovedFromContainerMessage>(OnRemovedFromContainer);
        SubscribeLocalEvent<OrdnanceCasingComponent, UseInHandEvent>(OnUseInHand);

        SubscribeLocalEvent<OrdnanceCasingComponent, AttemptEntityStickEvent>(OnAttemptStick);
        SubscribeLocalEvent<OrdnanceCasingComponent, EntityStuckEvent>(OnEntityStuck);
        SubscribeLocalEvent<OrdnanceCasingComponent, EntityUnstuckEvent>(OnEntityUnstuck);
        SubscribeLocalEvent<OrdnanceCasingComponent, ClaymoreDeployDoafterEvent>(OnClaymoreDeployed);
        SubscribeLocalEvent<OrdnanceCasingComponent, ClaymoreDisarmDoafterEvent>(OnClaymoreDisarmed);
        SubscribeLocalEvent<OrdnanceCasingComponent, OrdnanceDefuseDoAfterEvent>(OnDefuseDoAfter);
        SubscribeLocalEvent<OrdnanceCasingComponent, ContainerIsInsertingAttemptEvent>(OnContainerInserting);
        SubscribeLocalEvent<OrdnanceCasingComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(Entity<OrdnanceCasingComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled) return;

        if (TryComp<StickyComponent>(ent, out var sticky) && sticky.StuckTo != null)
        {
            if (!IsArmed(ent))
            {
                _sticky.UnstickFromEntity((ent.Owner, sticky), args.User);
                args.Handled = true;
            }
        }
    }

    private void OnContainerInserting(Entity<OrdnanceCasingComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.BallisticAmmoSlotId && !ent.Comp.IsLocked)
        {
            args.Cancel();
        }
    }

    private void OnAttemptStick(Entity<OrdnanceCasingComponent> ent, ref AttemptEntityStickEvent args)
    {
        if (!ent.Comp.IsLocked)
            args.Cancelled = true;

        if (ent.Comp.RequiredAssemblyMode == "Plastic" && args.Target != default)
        {
            if (_container.TryGetContainer(args.Target, "stickers_container", out var container))
            {
                foreach (var stuckEnt in container.ContainedEntities)
                {
                    if (HasComp<OrdnanceCasingComponent>(stuckEnt))
                    {
                        args.Cancelled = true;
                        if (args.User != default)
                            _popup.PopupClient(Loc.GetString("stories-ordnance-already-stuck"), ent, args.User);
                        return;
                    }
                }
            }
        }
    }

    public bool IsArmed(EntityUid uid)
    {
        if (HasComp<ActiveTimerTriggerComponent>(uid)) return true;
        if (TryComp<RMCLandmineComponent>(uid, out var mine) && mine.Armed) return true;
        if (!TryComp<OrdnanceCasingComponent>(uid, out var casing)) return false;

        if (_itemSlots.GetItemOrNull(uid, casing.TriggerSlotId) is { } holderUid &&
            TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
        {
            if (holder.Part1 != null)
            {
                if (TryComp<OrdnanceTimerComponent>(holder.Part1, out var t1) && t1.Enabled) return true;
                if (TryComp<OrdnanceProxSensorComponent>(holder.Part1, out var p1) && p1.Enabled) return true;
            }
            if (holder.Part2 != null)
            {
                if (TryComp<OrdnanceTimerComponent>(holder.Part2, out var t2) && t2.Enabled) return true;
                if (TryComp<OrdnanceProxSensorComponent>(holder.Part2, out var p2) && p2.Enabled) return true;
            }
        }
        return false;
    }

    private void OnDefuseDoAfter(Entity<OrdnanceCasingComponent> ent, ref OrdnanceDefuseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled) return;
        args.Handled = true;

        if (_net.IsServer)
        {
            var isFriendly = false;
            EntityUid? primer = null;

            if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is { } holderUid &&
                TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
            {
                if (holder.Part1 != null)
                {
                    if (TryComp<OrdnanceProxSensorComponent>(holder.Part1, out var p1) && p1.Primer != null) primer = p1.Primer;
                    else if (TryComp<OrdnanceTimerComponent>(holder.Part1, out var t1) && t1.Primer != null) primer = t1.Primer;
                }
                if (primer == null && holder.Part2 != null)
                {
                    if (TryComp<OrdnanceProxSensorComponent>(holder.Part2, out var p2) && p2.Primer != null) primer = p2.Primer;
                    else if (TryComp<OrdnanceTimerComponent>(holder.Part2, out var t2) && t2.Primer != null) primer = t2.Primer;
                }
            }

            if (primer != null)
            {
                if (primer == args.User) isFriendly = true;
                else if (_gunIff.TryGetFaction(primer.Value, out var faction) && _gunIff.IsInFaction(args.User, faction))
                    isFriendly = true;
            }

            if (!isFriendly && _random.Prob(0.75f))
            {
                _popup.PopupEntity(Loc.GetString("stories-ordnance-defuse-fail"), ent, args.User, PopupType.LargeCaution);
                var ev = new OrdnanceDetonateEvent(args.User);
                RaiseLocalEvent(ent, ref ev);
                return;
            }
        }

        _sticky.UnstickFromEntity((ent.Owner, Comp<StickyComponent>(ent.Owner)), args.User);
        _popup.PopupEntity(Loc.GetString("stories-ordnance-defuse-success"), ent, args.User);
    }

    private void OnEntityStuck(Entity<OrdnanceCasingComponent> ent, ref EntityStuckEvent args)
    {
        if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is { } holderUid &&
            TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
        {
            var ev = new OrdnancePrimeEvent(args.User);
            if (holder.Part1 != null) RaiseLocalEvent(holder.Part1.Value, ref ev);
            if (holder.Part2 != null) RaiseLocalEvent(holder.Part2.Value, ref ev);
        }
        UpdateAppearance(ent);
    }

    private void OnEntityUnstuck(Entity<OrdnanceCasingComponent> ent, ref EntityUnstuckEvent args)
    {
        UnprimeAssembly(ent);
        RemCompDeferred<ActiveTimerTriggerComponent>(ent);
        UpdateAppearance(ent);
    }

    private void OnClaymoreDeployed(Entity<OrdnanceCasingComponent> ent, ref ClaymoreDeployDoafterEvent args)
    {
        if (args.Cancelled) return;

        if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is { } holderUid &&
            TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
        {
            var ev = new OrdnancePrimeEvent(args.User);
            if (holder.Part1 != null) RaiseLocalEvent(holder.Part1.Value, ref ev);
            if (holder.Part2 != null) RaiseLocalEvent(holder.Part2.Value, ref ev);
        }
        UpdateAppearance(ent);
    }

    private void OnClaymoreDisarmed(Entity<OrdnanceCasingComponent> ent, ref ClaymoreDisarmDoafterEvent args)
    {
        if (args.Cancelled) return;
        UnprimeAssembly(ent);
        UpdateAppearance(ent);
    }

    private void UnprimeAssembly(Entity<OrdnanceCasingComponent> ent)
    {
        if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is { } holderUid &&
            TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
        {
            if (holder.Part1 != null)
            {
                if (TryComp<OrdnanceProxSensorComponent>(holder.Part1.Value, out var prox1))
                {
                    prox1.Enabled = false;
                    prox1.Armed = false;
                    prox1.TriggerDelayRemaining = 0;
                    Dirty(holder.Part1.Value, prox1);
                }
                if (TryComp<OrdnanceTimerComponent>(holder.Part1.Value, out var timer1))
                {
                    timer1.Enabled = false;
                    Dirty(holder.Part1.Value, timer1);
                }
            }

            if (holder.Part2 != null)
            {
                if (TryComp<OrdnanceProxSensorComponent>(holder.Part2.Value, out var prox2))
                {
                    prox2.Enabled = false;
                    prox2.Armed = false;
                    prox2.TriggerDelayRemaining = 0;
                    Dirty(holder.Part2.Value, prox2);
                }
                if (TryComp<OrdnanceTimerComponent>(holder.Part2.Value, out var timer2))
                {
                    timer2.Enabled = false;
                    Dirty(holder.Part2.Value, timer2);
                }
            }
        }
    }

    public OrdnanceCasingComponent GetEffectiveCasing(EntityUid uid, OrdnanceCasingComponent casing, out EntityUid effectiveUid)
    {
        if (casing.IsAssembly && _itemSlots.TryGetSlot(uid, casing.WarheadSlotId, out var warheadSlot) && warheadSlot.Item != null)
        {
            if (TryComp<OrdnanceCasingComponent>(warheadSlot.Item.Value, out var warheadCasing))
            {
                return GetEffectiveCasing(warheadSlot.Item.Value, warheadCasing, out effectiveUid);
            }
        }
        effectiveUid = uid;
        return casing;
    }

    public OrdnanceCasingComponent GetEffectiveCasing(EntityUid uid, OrdnanceCasingComponent casing)
    {
        return GetEffectiveCasing(uid, casing, out _);
    }

    public bool HasValidTrigger(EntityUid uid, OrdnanceCasingComponent comp)
    {
        if (!comp.IsAssembly)
            return true;

        if (_itemSlots.GetItemOrNull(uid, comp.TriggerSlotId) is not { } holderUid ||
            !TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
            return false;

        var igniters = 0;
        if (holder.Part1 != null && HasComp<OrdnanceIgniterComponent>(holder.Part1.Value)) igniters++;
        if (holder.Part2 != null && HasComp<OrdnanceIgniterComponent>(holder.Part2.Value)) igniters++;

        var hasTimer = (holder.Part1 != null && HasComp<OrdnanceTimerComponent>(holder.Part1.Value)) ||
                       (holder.Part2 != null && HasComp<OrdnanceTimerComponent>(holder.Part2.Value));

        var hasProx = (holder.Part1 != null && HasComp<OrdnanceProxSensorComponent>(holder.Part1.Value)) ||
                      (holder.Part2 != null && HasComp<OrdnanceProxSensorComponent>(holder.Part2.Value));

        var hasSignaller = (holder.Part1 != null && HasComp<OrdnanceSignallerComponent>(holder.Part1.Value)) ||
                           (holder.Part2 != null && HasComp<OrdnanceSignallerComponent>(holder.Part2.Value));

        return comp.RequiredAssemblyMode switch
        {
            "TimerIgniter" => hasTimer && igniters == 1,
            "DualIgniter" => igniters == 2,
            "Plastic" => (hasTimer || hasProx || hasSignaller) && igniters == 1,
            "Mine" => (hasProx && igniters == 1) || igniters == 2,
            "Any" => true,
            _ => false
        };
    }

    private void OnUseInHand(Entity<OrdnanceCasingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !ent.Comp.IsAssembly)
            return;

        if (ent.Comp.IsLocked && HasComp<StickyComponent>(ent))
        {
            if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is { } hUid)
            {
                var newEv = new UseInHandEvent(args.User);
                RaiseLocalEvent(hUid, newEv);
                if (newEv.Handled)
                    args.Handled = true;
            }
            return;
        }

        if (ent.Comp.IsLocked && !HasComp<RMCLandmineComponent>(ent))
        {
            if (HasComp<OnUseTimerTriggerComponent>(ent))
                return;

            if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is { } holderUid)
            {
                var ev = new OrdnancePrimeEvent(args.User);

                if (TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
                {
                    if (holder.Part1 != null) RaiseLocalEvent(holder.Part1.Value, ref ev);
                    if (holder.Part2 != null) RaiseLocalEvent(holder.Part2.Value, ref ev);
                }

                args.Handled = true;
                _popup.PopupClient(Loc.GetString("stories-ordnance-casing-primed"), ent, args.User);
                UpdateAppearance(ent);
            }
        }
    }

    private void OnInsertedIntoContainer(Entity<OrdnanceCasingComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateAppearance(ent);
    }

    private void OnRemovedFromContainer(Entity<OrdnanceCasingComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateAppearance(ent);
    }

    private void OnMapInit(Entity<OrdnanceCasingComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.IsAssembly)
            return;

        EnsureComp<ItemSlotsComponent>(ent);

        if (_tag.HasTag(ent, "RMCLaunchTube"))
        {
            if (!_itemSlots.TryGetSlot(ent, ent.Comp.FuelSlotId, out _))
            {
                _itemSlots.AddItemSlot(ent, ent.Comp.FuelSlotId, new ItemSlot
                {
                    Whitelist = new EntityWhitelist { Components = new[] { "FitsInDispenser" } },
                    Name = Loc.GetString("stories-ordnance-fuel-slot-name")
                });
            }
            return;
        }

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.BeakerSlot1Id, out _))
        {
            _itemSlots.AddItemSlot(ent, ent.Comp.BeakerSlot1Id, new ItemSlot
            {
                Whitelist = new EntityWhitelist { Components = new[] { "FitsInDispenser" } },
                Name = Loc.GetString("stories-ordnance-beaker-1-slot-name")
            });
        }

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.BeakerSlot2Id, out _))
        {
            _itemSlots.AddItemSlot(ent, ent.Comp.BeakerSlot2Id, new ItemSlot
            {
                Whitelist = new EntityWhitelist { Components = new[] { "FitsInDispenser" } },
                Name = Loc.GetString("stories-ordnance-beaker-2-slot-name")
            });
        }

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.TriggerSlotId, out _))
        {
            _itemSlots.AddItemSlot(ent, ent.Comp.TriggerSlotId, new ItemSlot
            {
                Whitelist = new EntityWhitelist { Components = new[] { "OrdnanceAssemblyHolder" } },
                Name = Loc.GetString("stories-ordnance-trigger-slot-name")
            });
        }
    }

    private void OnStartup(Entity<OrdnanceCasingComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnInteractUsing(Entity<OrdnanceCasingComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.IsAssembly)
            return;

        if (IsArmed(ent))
        {
            if (TryComp<StickyComponent>(ent, out var sticky) && sticky.StuckTo != null)
            {
                if (_tool.HasQuality(args.Used, "Pulsing"))
                {
                    args.Handled = true;
                    _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new OrdnanceDefuseDoAfterEvent(), ent, ent, args.Used)
                    {
                        BreakOnMove = true,
                        NeedHand = true
                    });
                    return;
                }
            }

            if (_tag.HasTag(args.Used, "Screwdriver"))
            {
                _popup.PopupClient(Loc.GetString("stories-mine-armed-cannot-open"), ent, args.User);
                return;
            }
        }

        if (_tag.HasTag(args.Used, "Screwdriver"))
        {
            args.Handled = true;
            ent.Comp.IsLocked = !ent.Comp.IsLocked;
            Dirty(ent);

            if (_tag.HasTag(ent, "RMCLaunchTube"))
            {
                _itemSlots.SetLock(ent, ent.Comp.FuelSlotId, ent.Comp.IsLocked);
                _itemSlots.SetLock(ent, ent.Comp.WarheadSlotId, ent.Comp.IsLocked);
            }
            else
            {
                _itemSlots.SetLock(ent, ent.Comp.BeakerSlot1Id, ent.Comp.IsLocked);
                _itemSlots.SetLock(ent, ent.Comp.BeakerSlot2Id, ent.Comp.IsLocked);
                _itemSlots.SetLock(ent, ent.Comp.TriggerSlotId, ent.Comp.IsLocked);
            }

            if (ent.Comp.IsLocked)
            {
                EntityManager.AddComponents(ent.Owner, ent.Comp.AddedComponents);

                foreach (var tag in ent.Comp.AddedTags)
                {
                    _tag.AddTag(ent.Owner, tag);
                }

                ConfigureTriggers(ent);

                var ev = new OrdnanceCasingLockedEvent();
                RaiseLocalEvent(ent.Owner, ref ev);
            }
            else
            {
                EntityManager.RemoveComponents(ent.Owner, ent.Comp.AddedComponents);

                foreach (var tag in ent.Comp.AddedTags)
                {
                    _tag.RemoveTag(ent.Owner, tag);
                }

                RemComp<OnUseTimerTriggerComponent>(ent);
            }

            var msg = ent.Comp.IsLocked
                ? Loc.GetString("stories-ordnance-casing-lock", ("casing", ent))
                : Loc.GetString("stories-ordnance-casing-unlock", ("casing", ent));
            _popup.PopupClient(msg, ent, args.User);

            UpdateAppearance(ent);
        }
    }

    private void ConfigureTriggers(Entity<OrdnanceCasingComponent> ent)
    {
        if (!ent.Comp.IsAssembly)
            return;

        if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is not { } holderUid ||
            !TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
            return;

        var igniterCount = 0;

        if (holder.Part1 != null && HasComp<OrdnanceIgniterComponent>(holder.Part1.Value)) igniterCount++;
        if (holder.Part2 != null && HasComp<OrdnanceIgniterComponent>(holder.Part2.Value)) igniterCount++;

        if (igniterCount == 2 && ent.Comp.DualIgniterConeAngle != null)
        {
            ent.Comp.UseDirection = true;
            ent.Comp.ConeAngle = ent.Comp.DualIgniterConeAngle.Value;
            Dirty(ent);
        }
        else
        {
            ent.Comp.UseDirection = false;
            Dirty(ent);
        }

        if (TryComp<StepTriggerComponent>(ent, out var step))
        {
            _stepTrigger.SetActive(ent, igniterCount == 2, step);
        }
    }

    private void OnGetAltVerbs(Entity<OrdnanceCasingComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.HasBlastDampener || !ent.Comp.IsAssembly)
            return;

        if (!ent.Comp.IsLocked || IsArmed(ent))
            return;

        var user = args.User;

        if (HasComp<Content.Shared._RMC14.Xenonids.XenoComponent>(user))
            return;

        if (ent.Comp.DampenerSkills != null && !_skills.HasSkills(user, ent.Comp.DampenerSkills))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("stories-ordnance-toggle-dampener"),
            Act = () =>
            {
                ent.Comp.BlastDampener = !ent.Comp.BlastDampener;
                Dirty(ent);
                var msg = ent.Comp.BlastDampener
                    ? Loc.GetString("stories-ordnance-casing-dampener-enabled")
                    : Loc.GetString("stories-ordnance-casing-dampener-disabled");
                _popup.PopupClient(msg, ent.Owner, user);
            }
        });
    }

    private void OnExamined(Entity<OrdnanceCasingComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.IsAssembly)
            return;

        var lockedMsg = ent.Comp.IsLocked
            ? Loc.GetString("stories-ordnance-casing-examine-locked")
            : Loc.GetString("stories-ordnance-casing-examine-unlocked");
        args.PushMarkup(lockedMsg);

        if (ent.Comp.HasBlastDampener)
        {
            var dampenerMsg = ent.Comp.BlastDampener
                ? Loc.GetString("stories-ordnance-casing-examine-dampener-on")
                : Loc.GetString("stories-ordnance-casing-examine-dampener-off");
            args.PushMarkup(dampenerMsg);
        }
    }

    private void OnInsertAttempt(Entity<OrdnanceCasingComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot == null || !ent.Comp.IsAssembly)
            return;

        if (args.Slot.ID == ent.Comp.TriggerSlotId)
        {
            if (!TryComp<OrdnanceAssemblyHolderComponent>(args.Item, out var holder))
            {
                args.Cancelled = true;
                return;
            }

            if (!holder.IsLocked)
            {
                args.Cancelled = true;
                if (args.User != null)
                    _popup.PopupClient(Loc.GetString("stories-assembly-not-secured"), ent, args.User.Value);
                return;
            }

            var igniters = 0;
            if (holder.Part1 != null && HasComp<OrdnanceIgniterComponent>(holder.Part1.Value)) igniters++;
            if (holder.Part2 != null && HasComp<OrdnanceIgniterComponent>(holder.Part2.Value)) igniters++;

            var hasTimer = (holder.Part1 != null && HasComp<OrdnanceTimerComponent>(holder.Part1.Value)) ||
                           (holder.Part2 != null && HasComp<OrdnanceTimerComponent>(holder.Part2.Value));

            var hasProx = (holder.Part1 != null && HasComp<OrdnanceProxSensorComponent>(holder.Part1.Value)) ||
                          (holder.Part2 != null && HasComp<OrdnanceProxSensorComponent>(holder.Part2.Value));

            var hasSignaller = (holder.Part1 != null && HasComp<OrdnanceSignallerComponent>(holder.Part1.Value)) ||
                               (holder.Part2 != null && HasComp<OrdnanceSignallerComponent>(holder.Part2.Value));

            var isValid = ent.Comp.RequiredAssemblyMode switch
            {
                "TimerIgniter" => hasTimer && igniters == 1,
                "DualIgniter" => igniters == 2,
                "Plastic" => (hasTimer || hasProx || hasSignaller) && igniters == 1,
                "Mine" => (hasProx && igniters == 1) || igniters == 2,
                "Any" => true,
                _ => false
            };

            if (!isValid)
            {
                args.Cancelled = true;
                if (args.User != null)
                    _popup.PopupClient(Loc.GetString("stories-ordnance-invalid-trigger"), ent, args.User.Value);
            }
        }
        else if (args.Slot.ID == ent.Comp.WarheadSlotId)
        {
            if (TryComp<OrdnanceCasingComponent>(args.Item, out var warheadComp) && !warheadComp.IsLocked)
            {
                args.Cancelled = true;
                if (args.User != null)
                    _popup.PopupClient(Loc.GetString("stories-warhead-not-secured"), ent, args.User.Value);
            }
        }
        else if (args.Slot.ID == ent.Comp.BeakerSlot1Id || args.Slot.ID == ent.Comp.BeakerSlot2Id || args.Slot.ID == ent.Comp.FuelSlotId)
        {
            if (_solutionContainer.TryGetFitsInDispenser(args.Item, out _, out var solution))
            {
                var currentVolume = FixedPoint2.Zero;

                if (args.Slot.ID == ent.Comp.BeakerSlot1Id || args.Slot.ID == ent.Comp.BeakerSlot2Id)
                {
                    if (args.Slot.ID != ent.Comp.BeakerSlot1Id && _itemSlots.TryGetSlot(ent, ent.Comp.BeakerSlot1Id, out var slot1) && slot1.Item != null && _solutionContainer.TryGetFitsInDispenser(slot1.Item.Value, out _, out var sol1))
                        currentVolume += sol1.MaxVolume;
                    if (args.Slot.ID != ent.Comp.BeakerSlot2Id && _itemSlots.TryGetSlot(ent, ent.Comp.BeakerSlot2Id, out var slot2) && slot2.Item != null && _solutionContainer.TryGetFitsInDispenser(slot2.Item.Value, out _, out var sol2))
                        currentVolume += sol2.MaxVolume;
                }

                if (currentVolume + solution.MaxVolume > ent.Comp.MaxVolume)
                {
                    args.Cancelled = true;
                    if (args.User != null)
                        _popup.PopupClient(Loc.GetString("stories-ordnance-casing-container-too-big"), ent, args.User.Value);
                }
            }
        }
    }

    public void UpdateAppearance(Entity<OrdnanceCasingComponent> ent)
    {
        if (!ent.Comp.UpdateAppearance || !ent.Comp.IsAssembly)
            return;

        string state;

        var isTicking = HasComp<ActiveTimerTriggerComponent>(ent);
        var isTimerEnabled = false;
        var isProxEnabled = false;

        if (_itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) is { } holderUid &&
            TryComp<OrdnanceAssemblyHolderComponent>(holderUid, out var holder))
        {
            if (holder.Part1 != null && TryComp<OrdnanceTimerComponent>(holder.Part1, out var t1) && t1.Enabled) isTimerEnabled = true;
            if (holder.Part2 != null && TryComp<OrdnanceTimerComponent>(holder.Part2, out var t2) && t2.Enabled) isTimerEnabled = true;
            if (holder.Part1 != null && TryComp<OrdnanceProxSensorComponent>(holder.Part1, out var p1) && p1.Enabled) isProxEnabled = true;
            if (holder.Part2 != null && TryComp<OrdnanceProxSensorComponent>(holder.Part2, out var p2) && p2.Enabled) isProxEnabled = true;
        }

        var isMineArmed = TryComp<RMCLandmineComponent>(ent, out var mine) && mine.Armed;
        var isStuck = TryComp<StickyComponent>(ent, out var sticky) && sticky.StuckTo != null;

        if (_tag.HasTag(ent, "RMCLaunchTube"))
        {
            var hasWarhead = _itemSlots.GetItemOrNull(ent, ent.Comp.WarheadSlotId) != null;
            var hasFuel = HasFuel(ent.Owner, ent.Comp);

            if (!hasWarhead)
                state = "icon";
            else if (!hasFuel)
                state = "icon_no_fuel";
            else if (!ent.Comp.IsLocked)
                state = "icon_unlocked";
            else
                state = "icon_locked";
        }
        else
        {
            var hasTrigger = _itemSlots.GetItemOrNull(ent, ent.Comp.TriggerSlotId) != null;

            if (isTicking || isTimerEnabled)
            {
                state = "icon_active";
            }
            else if (isMineArmed)
            {
                state = "icon_active";
            }
            else if (isProxEnabled)
            {
                state = "icon_sensing";
            }
            else if (isStuck)
            {
                state = "icon_active";
            }
            else if (ent.Comp.IsLocked)
            {
                state = "icon_locked";
            }
            else if (hasTrigger)
            {
                state = "icon_ass";
            }
            else
            {
                state = "icon";
            }
        }

        _appearance.SetData(ent, OrdnanceCasingVisuals.StateId, state);

        _sticky.SetCanUnstick(ent, !IsArmed(ent));
    }

    public bool HasFuel(EntityUid uid, OrdnanceCasingComponent? casing = null)
    {
        if (!Resolve(uid, ref casing, false))
            return true;

        if (casing.RequiredFuelReagent == null)
            return true;

        if (_itemSlots.TryGetSlot(uid, casing.FuelSlotId, out var fuelSlot) && fuelSlot.Item != null &&
            _solutionContainer.TryGetFitsInDispenser(fuelSlot.Item.Value, out _, out var fuelSol))
        {
            var fuelAmount = FixedPoint2.Zero;
            foreach (var content in fuelSol.Contents)
            {
                if (content.Reagent.Prototype == casing.RequiredFuelReagent)
                    fuelAmount += content.Quantity;
            }

            return fuelAmount >= casing.RequiredFuelAmount;
        }

        return false;
    }

    public bool TryConsumeFuel(EntityUid uid, OrdnanceCasingComponent? casing = null)
    {
        if (!Resolve(uid, ref casing, false))
            return false;

        if (casing.RequiredFuelReagent == null)
            return true;

        if (_itemSlots.TryGetSlot(uid, casing.FuelSlotId, out var fuelSlot) && fuelSlot.Item != null &&
            _solutionContainer.TryGetFitsInDispenser(fuelSlot.Item.Value, out var fuelSolnUid, out var fuelSol))
        {
            var fuelAmount = FixedPoint2.Zero;
            foreach (var content in fuelSol.Contents)
            {
                if (content.Reagent.Prototype == casing.RequiredFuelReagent)
                    fuelAmount += content.Quantity;
            }

            if (fuelAmount >= casing.RequiredFuelAmount && fuelSolnUid.HasValue)
            {
                _solutionContainer.RemoveReagent(fuelSolnUid.Value, casing.RequiredFuelReagent, casing.RequiredFuelAmount);
                UpdateAppearance((uid, casing));
                return true;
            }
        }

        return false;
    }
}

[Serializable, NetSerializable]
public sealed partial class OrdnanceDefuseDoAfterEvent : SimpleDoAfterEvent { }
