using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Blood;

[Flags]
public enum BloodStainParts : byte
{
    None = 0,
    Body = 1 << 0,
    Hands = 1 << 1,
    Feet = 1 << 2,
    Head = 1 << 3,
}

[Serializable, NetSerializable]
public sealed partial class BloodCleanDoAfterEvent : SimpleDoAfterEvent;

public sealed class BloodStainSystem : EntitySystem
{
    public const string HeadSlot = "head";
    public const string MaskSlot = "mask";
    public const string GlovesSlot = "gloves";
    public const string ShoesSlot = "shoes";
    public const string OuterSlot = "outerClothing";
    public const string JumpsuitSlot = "jumpsuit";

    private static readonly Dictionary<string, string> SlotWornStates = new()
    {
        [HeadSlot] = "helmet_blood",
        [MaskSlot] = "mask_blood",
        [GlovesSlot] = "hands_blood",
        [ShoesSlot] = "feet_blood",
        [OuterSlot] = "armor_blood",
        [JumpsuitSlot] = "uniform_blood",
    };

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCReagentSystem _reagent = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodStainComponent, ExaminedEvent>(OnStainExamined);
        SubscribeLocalEvent<BloodyComponent, ExaminedEvent>(OnBloodyExamined);
        SubscribeLocalEvent<BloodyComponent, DidEquipHandEvent>(OnDidEquipHand);
    }

    private void OnStainExamined(Entity<BloodStainComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Color is not { } color)
            return;

        args.PushMarkup(Loc.GetString("stories-blood-stain-examine-item", ("color", color.ToHex())));
    }

    private void OnBloodyExamined(Entity<BloodyComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.HandsColor is { } hands && !_inventory.TryGetSlotEntity(ent, GlovesSlot, out _))
            args.PushMarkup(Loc.GetString("stories-blood-stain-examine-hands", ("color", hands.ToHex()), ("target", ent.Owner)));

        if (ent.Comp.FeetColor is { } feet && !_inventory.TryGetSlotEntity(ent, ShoesSlot, out _))
            args.PushMarkup(Loc.GetString("stories-blood-stain-examine-feet", ("color", feet.ToHex()), ("target", ent.Owner)));
    }

    private void OnDidEquipHand(Entity<BloodyComponent> ent, ref DidEquipHandEvent args)
    {
        if (_net.IsClient || ent.Comp.HandTransfers <= 0)
            return;

        Color? color;
        if (_inventory.TryGetSlotEntity(ent, GlovesSlot, out var gloves))
            color = CompOrNull<BloodStainComponent>(gloves)?.Color;
        else
            color = ent.Comp.HandsColor;

        if (color is { } handColor && TryStain(args.Equipped, handColor))
            ent.Comp.HandTransfers--;
    }

    public string? GetWornState(Entity<BloodStainComponent> ent, string slot)
    {
        if (ent.Comp.WornState != null)
            return ent.Comp.WornState;

        return SlotWornStates.GetValueOrDefault(slot);
    }

    public bool TryGetBloodColor(EntityUid mob, out Color color)
    {
        if (CompOrNull<BloodyComponent>(mob)?.BloodColor is { } overrideColor)
        {
            color = overrideColor;
            return true;
        }

        color = default;
        if (!TryComp<BloodstreamComponent>(mob, out var bloodstream) ||
            !_reagent.TryIndex(bloodstream.BloodReagent, out var reagent))
        {
            return false;
        }

        color = reagent.SubstanceColor;
        return true;
    }

    public bool TryStain(EntityUid item, Color color)
    {
        if (_net.IsClient || !HasComp<ItemComponent>(item))
            return false;

        var stain = EnsureComp<BloodStainComponent>(item);
        if (!stain.CanStain || stain.Color == color)
            return false;

        stain.Color = color;
        Dirty(item, stain);

        if (!HasComp<ReactiveComponent>(item))
        {
            AddComp<ReactiveComponent>(item);
            stain.AddedReactive = true;
        }

        return true;
    }

    public bool TryClean(EntityUid item)
    {
        if (_net.IsClient || !TryComp<BloodStainComponent>(item, out var stain) || stain.Color == null)
            return false;

        stain.Color = null;
        Dirty(item, stain);

        if (stain.AddedReactive)
        {
            RemComp<ReactiveComponent>(item);
            stain.AddedReactive = false;
        }

        return true;
    }

    public bool IsCleaningReagent(Entity<BloodStainComponent> ent, ProtoId<ReagentPrototype> reagent)
    {
        return ent.Comp.CleaningReagents.Contains(reagent);
    }

    public void StainWearer(Entity<BloodyComponent?> wearer, BloodStainParts parts, Color color)
    {
        if (_net.IsClient || !Resolve(wearer, ref wearer.Comp, false) || !wearer.Comp.CanBeStained)
            return;

        var bloody = wearer.Comp;
        var dirty = false;

        if ((parts & BloodStainParts.Body) != 0)
        {
            if (_inventory.TryGetSlotEntity(wearer, OuterSlot, out var outer))
                TryStain(outer.Value, color);
            else if (_inventory.TryGetSlotEntity(wearer, JumpsuitSlot, out var jumpsuit))
                TryStain(jumpsuit.Value, color);
        }

        if ((parts & BloodStainParts.Head) != 0)
        {
            if (_inventory.TryGetSlotEntity(wearer, HeadSlot, out var head))
                TryStain(head.Value, color);

            if (_inventory.TryGetSlotEntity(wearer, MaskSlot, out var mask))
                TryStain(mask.Value, color);
        }

        if ((parts & BloodStainParts.Hands) != 0)
        {
            if (_inventory.TryGetSlotEntity(wearer, GlovesSlot, out var gloves))
            {
                TryStain(gloves.Value, color);
            }
            else if (bloody.HandsColor != color)
            {
                bloody.HandsColor = color;
                dirty = true;
            }

            bloody.HandTransfers = _random.Next(bloody.MinHandTransfers, bloody.MaxHandTransfers + 1);
        }

        if ((parts & BloodStainParts.Feet) != 0)
        {
            if (_inventory.TryGetSlotEntity(wearer, ShoesSlot, out var shoes))
            {
                TryStain(shoes.Value, color);
            }
            else if (bloody.FeetColor != color)
            {
                bloody.FeetColor = color;
                dirty = true;
            }
        }

        if (dirty)
            Dirty(wearer, bloody);
    }

    public bool CleanWearer(Entity<BloodyComponent?> wearer)
    {
        if (_net.IsClient || !Resolve(wearer, ref wearer.Comp, false))
            return false;

        var cleaned = false;
        foreach (var slot in SlotWornStates.Keys)
        {
            if (_inventory.TryGetSlotEntity(wearer, slot, out var item))
                cleaned |= TryClean(item.Value);
        }

        var bloody = wearer.Comp;
        bloody.HandTransfers = 0;
        bloody.Steps = 0;
        bloody.DragSteps = 0;
        if (bloody.HandsColor != null || bloody.FeetColor != null)
        {
            bloody.HandsColor = null;
            bloody.FeetColor = null;
            Dirty(wearer, bloody);
            cleaned = true;
        }

        return cleaned;
    }

    public bool IsWearerBloody(Entity<BloodyComponent?> wearer)
    {
        if (!Resolve(wearer, ref wearer.Comp, false))
            return false;

        if (wearer.Comp.HandsColor != null || wearer.Comp.FeetColor != null)
            return true;

        foreach (var slot in SlotWornStates.Keys)
        {
            if (_inventory.TryGetSlotEntity(wearer, slot, out var item) &&
                CompOrNull<BloodStainComponent>(item)?.Color != null)
            {
                return true;
            }
        }

        return false;
    }
}
