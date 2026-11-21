using Content.Shared.Item;
using Content.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Requisitions;

/// <summary>
/// Uses the same size-based allocation for platform previews and delivered requisitions.
/// Each parcel is filled with the largest remaining products that fit its remaining capacity.
/// </summary>
public static class RequisitionsShipping
{
    // The physical consolidated shipment prototype has this many entity-storage slots.
    public const int MaxShipmentItems = 34;

    public static int GetWeight(EntProtoId product, IPrototypeManager prototypes, IComponentFactory factory)
    {
        if (!prototypes.TryIndex(product, out var prototype) ||
            !prototype.TryGetComponent<ItemComponent>(out var item, factory) ||
            !prototypes.TryIndex(item.Size, out var size))
            return int.MaxValue;
        return Math.Max(1, size.Weight);
    }

    public static int Count(
        IReadOnlyList<RequisitionsShippingItem> items, int weightLimit, int itemLimit,
        IPrototypeManager prototypes, IComponentFactory factory)
    {
        return Count(items, weightLimit, itemLimit, prototypes, factory, out _);
    }

    public static int Count(
        IReadOnlyList<RequisitionsShippingItem> items, int weightLimit, int itemLimit,
        IPrototypeManager prototypes, IComponentFactory factory, out int separate)
    {
        return Allocate(items, weightLimit, itemLimit, prototypes, factory, null, out separate);
    }

    public static List<RequisitionsEntry> CreateDeliveries(
        IReadOnlyList<RequisitionsShippingItem> items, EntProtoId container, int weightLimit, int itemLimit,
        IPrototypeManager prototypes, IComponentFactory factory, RequisitionsOrderDetails? request = null)
    {
        var deliveries = new List<RequisitionsEntry>();
        Allocate(items, weightLimit, itemLimit, prototypes, factory, (package, index, quantity, packed) =>
        {
            var item = items[index];
            var product = item.Entry;
            if (package == deliveries.Count)
            {
                deliveries.Add(packed
                    ? new RequisitionsEntry
                    {
                        Crate = container, Consolidated = true, BlackMarket = product.BlackMarket, Request = request,
                    }
                    : new RequisitionsEntry
                    {
                        Crate = product.Crate, Cost = product.Cost, Name = product.Name,
                        Description = product.Description, Icon = product.Icon,
                        Entities = new List<EntProtoId>(product.Entities), Request = request,
                        BlackMarket = product.BlackMarket, BlackMarketCost = product.BlackMarketCost,
                        BlackMarketHeat = product.BlackMarketHeat,
                        Unit = product.Unit,
                    });
            }
            var delivery = deliveries[package];
            if (packed)
            {
                for (var number = 0; number < quantity; number++)
                    delivery.Entities.Add(product.Crate);
                delivery.Cost += product.Cost * quantity;
                delivery.BlackMarketCost += product.BlackMarketCost * quantity;
            }
            if (item.RequestLine < 0)
                return;
            var line = delivery.RequestLines.Find(line => line.Line == item.RequestLine);
            if (line == null)
                delivery.RequestLines.Add(new RequisitionsDeliveryLine(item.RequestLine, quantity));
            else
                line.Amount += quantity;
        }, out _);
        return deliveries;
    }

    private static int Allocate(
        IReadOnlyList<RequisitionsShippingItem> items, int weightLimit, int itemLimit,
        IPrototypeManager prototypes, IComponentFactory factory, Action<int, int, int, bool>? place, out int separate)
    {
        separate = 0;
        weightLimit = Math.Max(1, weightLimit);
        itemLimit = Math.Clamp(itemLimit, 1, MaxShipmentItems);
        var pending = new List<ShippingLine>();
        var packages = 0;
        long remaining = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Quantity <= 0)
                continue;
            var weight = item.Entry.Unit ? GetWeight(item.Entry.Crate, prototypes, factory) : int.MaxValue;
            if (weight == int.MaxValue || weight > weightLimit)
            {
                if (item.Quantity > int.MaxValue - packages)
                    return int.MaxValue;
                if (place != null)
                {
                    for (var number = 0; number < item.Quantity; number++)
                        place(packages + number, index, 1, false);
                }
                packages += item.Quantity;
                separate += item.Quantity;
                continue;
            }
            pending.Add(new ShippingLine(index, item.Entry.Crate, weight, item.Quantity));
            remaining += item.Quantity;
        }
        pending.Sort((left, right) =>
        {
            var comparison = right.Weight.CompareTo(left.Weight);
            if (comparison != 0)
                return comparison;
            comparison = string.CompareOrdinal(left.Prototype.Id, right.Prototype.Id);
            return comparison != 0 ? comparison : left.Index.CompareTo(right.Index);
        });
        while (remaining > 0)
        {
            if (packages == int.MaxValue)
                return int.MaxValue;
            var room = weightLimit;
            var slots = itemLimit;
            foreach (var line in pending)
            {
                var quantity = Math.Min(line.Remaining, Math.Min(slots, room / line.Weight));
                if (quantity == 0)
                    continue;
                place?.Invoke(packages, line.Index, quantity, true);
                line.Remaining -= quantity;
                remaining -= quantity;
                slots -= quantity;
                room -= quantity * line.Weight;
            }
            packages++;
        }
        return packages;
    }

    private sealed class ShippingLine(int index, EntProtoId prototype, int weight, int quantity)
    {
        public readonly int Index = index;
        public readonly EntProtoId Prototype = prototype;
        public readonly int Weight = weight;
        public int Remaining = quantity;
    }
}

public readonly record struct RequisitionsShippingItem(RequisitionsEntry Entry, int Quantity, int RequestLine = -1);
