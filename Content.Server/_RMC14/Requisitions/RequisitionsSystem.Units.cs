using System.Linq;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Item;
using Content.Shared.Power;
using Content.Shared.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    [Dependency] private readonly SharedRMCPowerSystem _requisitionPower = default!;
    [Dependency] private readonly SharedCMAutomatedVendorSystem _vendors = default!;

    private bool _stockDirty;
    private TimeSpan _nextStockRefresh;

    private void InitializeUnits()
    {
        SubscribeLocalEvent<RequisitionsStockChangedEvent>(OnStockChanged);
        SubscribeLocalEvent<RequisitionsStockSourceComponent, PowerChangedEvent>(OnSupplyPowerChanged);
        SubscribeLocalEvent<RequisitionsStockSourceComponent, MapInitEvent>(OnSupplyMapInit);
        SubscribeLocalEvent<RequisitionsStockSourceComponent, ComponentShutdown>(OnSupplyShutdown);
    }

    private void OnSupplyPowerChanged(Entity<RequisitionsStockSourceComponent> vendor, ref PowerChangedEvent args)
    {
        _stockDirty = true;
    }

    private void OnSupplyMapInit(Entity<RequisitionsStockSourceComponent> vendor, ref MapInitEvent args)
    {
        _stockDirty = true;
    }

    private void OnSupplyShutdown(Entity<RequisitionsStockSourceComponent> vendor, ref ComponentShutdown args)
    {
        _stockDirty = true;
    }

    private void OnStockChanged(ref RequisitionsStockChangedEvent args)
    {
        _stockDirty = true;
    }

    private void BuildUnitCatalog(Entity<RequisitionsComputerComponent> computer)
    {
        if (computer.Comp.UnitsBuilt)
            return;

        var prices = RequisitionsUnitCatalog.GetPrices(computer.Comp.Categories, _prototype,
            EntityManager.ComponentFactory, out var productCategories, computer.Comp.UnitPrices);

        computer.Comp.Categories = computer.Comp.Categories.Select(category => new RequisitionsCategory
        {
            Name = category.Name,
            Entries = new List<RequisitionsEntry>(category.Entries),
        }).ToList();
        foreach (var (product, price) in prices.OrderBy(pair => pair.Key.Id))
        {
            var category = productCategories.GetValueOrDefault(product, "rmc-requisitions-category-supplies");
            GetUnitCategory(computer, category).Entries.Add(new RequisitionsEntry
            {
                Crate = product, Cost = price, Unit = true,
            });
        }

        computer.Comp.UnitsBuilt = true;
        RefreshUnitStock(computer);
        Dirty(computer);
    }

    private static RequisitionsCategory GetUnitCategory(Entity<RequisitionsComputerComponent> computer, LocId name)
    {
        var category = computer.Comp.Categories.FirstOrDefault(category => category.Name == name);
        if (category != null)
            return category;

        category = new RequisitionsCategory { Name = name };
        computer.Comp.Categories.Add(category);
        return category;
    }

    private List<Entity<CMAutomatedVendorComponent>> GetSupplyVendors(Entity<RequisitionsComputerComponent> computer)
    {
        var result = new List<Entity<CMAutomatedVendorComponent>>();
        var origin = Transform(computer);
        var query =
            EntityQueryEnumerator<RequisitionsStockSourceComponent, CMAutomatedVendorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var vendor, out var xform))
        {
            if (xform.MapID != origin.MapID || xform.GridUid != origin.GridUid ||
                TerminatingOrDeleted(uid) || !_requisitionPower.IsPowered(uid))
                continue;

            result.Add((uid, vendor));
        }
        result.Sort((a, b) => a.Owner.CompareTo(b.Owner));
        return result;
    }

    private Dictionary<EntProtoId, int> ReadSupplyStock(
        Entity<RequisitionsComputerComponent> computer,
        EntityUid? actor = null,
        Dictionary<EntProtoId, LocId>? categories = null)
    {
        var stock = new Dictionary<EntProtoId, int>();
        foreach (var vendor in GetSupplyVendors(computer))
        {
            if (actor is { } user && !_access.IsAllowed(user, vendor))
                continue;

            foreach (var section in vendor.Comp.Sections)
            {
                foreach (var entry in section.Entries)
                {
                    if (!SharedCMAutomatedVendorSystem.IsRequisitionEntry(section, entry) ||
                        !_prototype.TryIndex(entry.Id, out var prototype) ||
                        !prototype.HasComponent<ItemComponent>(EntityManager.ComponentFactory) ||
                        prototype.HasComponent<CMVendorBundleComponent>(EntityManager.ComponentFactory) ||
                        prototype.HasComponent<CMChangeUserOnVendComponent>(EntityManager.ComponentFactory))
                        continue;

                    var amount = _vendors.GetRequisitionAmount(vendor, section, entry);
                    stock[entry.Id] = (int) Math.Min(int.MaxValue, (long) stock.GetValueOrDefault(entry.Id) + amount);
                    if (categories != null)
                    {
                        categories.TryAdd(entry.Id,
                            section.RequisitionCategory ?? Comp<RequisitionsStockSourceComponent>(vendor).Category);
                    }
                }
            }
        }
        return stock;
    }

    private void RefreshUnitStock(Entity<RequisitionsComputerComponent> computer)
    {
        var categories = new Dictionary<EntProtoId, LocId>();
        computer.Comp.Stock = ReadSupplyStock(computer, categories: categories);
        if (!computer.Comp.UnitsBuilt)
            return;

        var known = computer.Comp.Categories.SelectMany(category => category.Entries)
            .Where(entry => entry.Unit).Select(entry => entry.Crate).ToHashSet();
        foreach (var product in computer.Comp.Stock.Keys.OrderBy(id => id.Id))
        {
            if (known.Add(product))
                GetUnitCategory(computer, categories[product]).Entries.Add(new RequisitionsEntry
                {
                    Crate = product, Cost = 0, Unit = true,
                });
        }
        Dirty(computer);
    }

    private void UpdateUnitStock()
    {
        if (!_stockDirty || _timing.CurTime < _nextStockRefresh)
            return;

        _stockDirty = false;
        _nextStockRefresh = _timing.CurTime + TimeSpan.FromSeconds(1);
        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
            RefreshUnitStock((uid, computer));
    }

    private int IssueUnits(
        Entity<RequisitionsComputerComponent> computer, EntityUid actor, EntProtoId product, int amount)
    {
        var issued = 0;
        foreach (var vendor in GetSupplyVendors(computer))
        {
            for (var section = 0; section < vendor.Comp.Sections.Count && issued < amount; section++)
            {
                var entries = vendor.Comp.Sections[section].Entries;
                for (var entry = 0; entry < entries.Count && issued < amount; entry++)
                {
                    if (entries[entry].Id != product)
                        continue;

                    while (issued < amount && _vendors.TryIssueRequisition(vendor, actor, section, entry))
                        issued++;
                }
            }
        }
        return issued;
    }

    private List<RequisitionsEntry> PrepareShipments(
        Entity<RequisitionsComputerComponent> computer,
        List<(RequisitionsEntry Product, int Amount, int Line)> purchases,
        RequisitionsOrderDetails? details = null)
    {
        var orders = new List<RequisitionsEntry>();
        RequisitionsEntry? parcel = null;
        var capacity = Math.Clamp(computer.Comp.ShipmentItems, 1, 20);
        foreach (var (product, amount, line) in purchases)
        {
            for (var i = 0; i < amount; i++)
            {
                if (!product.Unit)
                {
                    var order = new RequisitionsEntry
                    {
                        Crate = product.Crate, Cost = product.Cost, Name = product.Name,
                        Description = product.Description, Icon = product.Icon,
                        Entities = new List<EntProtoId>(product.Entities), Request = details,
                    };
                    if (line >= 0)
                        order.RequestLines.Add(new RequisitionsDeliveryLine(line, 1));
                    orders.Add(order);
                    continue;
                }

                if (parcel == null || parcel.Entities.Count >= capacity)
                {
                    parcel = new RequisitionsEntry
                    {
                        Crate = computer.Comp.UnitShipment, NoReturnReward = true, Request = details,
                    };
                    orders.Add(parcel);
                }
                parcel.Entities.Add(product.Crate);
                parcel.Cost += product.Cost;
                if (line >= 0)
                {
                    var delivery = parcel.RequestLines.Find(value => value.Line == line);
                    if (delivery == null)
                        parcel.RequestLines.Add(new RequisitionsDeliveryLine(line, 1));
                    else
                        delivery.Amount++;
                }
            }
        }
        return orders;
    }

    private void CompleteRequestDelivery(RequisitionsEntry order)
    {
        if (order.Request == null || order.RequestLines.Count == 0 ||
            !TryGetEntity(order.Request.Account, out var accountUid) ||
            !TryComp(accountUid, out RequisitionsAccountComponent? account) ||
            account.Requests.Find(request => request.Number == order.Request.Number) is not { } request)
            return;

        foreach (var delivery in order.RequestLines)
        {
            if (delivery.Line < 0 || delivery.Line >= request.Items.Count)
                continue;
            var item = request.Items[delivery.Line];
            var amount = Math.Min(item.InTransit, delivery.Amount);
            item.InTransit -= amount;
            item.Fulfilled += amount;
        }
        request.Revision++;
        UpdateRequestPapers(accountUid.Value, request);
        if (request.Closed && request.Items.All(item => item.InTransit == 0))
            account.Requests.Remove(request);
    }
}
