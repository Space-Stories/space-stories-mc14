using Content.Shared._Stories.Synth;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Stories.Synth;

public sealed class STJobVariantGearSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STJobVariantGearComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(Entity<STJobVariantGearComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        if (!args.Profile.VariantPreferences.TryGetValue(args.JobId, out var variant) || variant == null)
            return;

        if (!ent.Comp.Variants.TryGetValue(variant, out var options) || options.Count == 0)
            return;

        var mob = args.Mob;
        var slot = ent.Comp.Slot;

        var held = new List<(string Slot, EntityUid Item)>();
        foreach (var depSlot in ent.Comp.DependentSlots)
        {
            if (_inventory.TryUnequip(mob, depSlot, out var item, force: true) && item is { } heldItem)
                held.Add((depSlot, heldItem));
        }

        if (_inventory.TryUnequip(mob, slot, out var oldItem, force: true) && oldItem is { } old)
            QueueDel(old);

        var chosenProto = _random.Pick(options);
        var newItem = Spawn(chosenProto, MapCoordinates.Nullspace);
        _inventory.TryEquip(mob, newItem, slot, force: true);

        foreach (var (depSlot, item) in held)
        {
            _inventory.TryEquip(mob, item, depSlot, force: true);
        }

        _movementSpeed.RefreshMovementSpeedModifiers(mob);

        RaiseLocalEvent(mob, new STJobVariantGearAppliedEvent(variant));
    }
}
