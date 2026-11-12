using Content.Shared.Item;
using Content.Shared.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;

namespace Content.Shared._RMC14.Vendors;

public abstract partial class SharedCMAutomatedVendorSystem
{
    public static bool IsRequisitionEntry(CMVendorSection section, CMVendorEntry entry)
    {
        // Alternate package buttons share a counter. Only expose the underlying stock item.
        return entry.Box == null && entry.Points == null && entry.Spawn == 1 && entry.LinkedEntries.Count == 0 &&
               entry.GiveSquadRoleName == null && entry.GiveIcon == null && entry.GivePrefix == null &&
               entry.GiveMapBlip == null &&
               section.Choices == null && section.TakeAll == null && section.TakeOne == null &&
               section.SharedSpecLimit == null && section.Jobs.Count == 0 && section.Ranks.Count == 0 &&
               section.Holidays.Count == 0;
    }

    public int GetRequisitionAmount(
        Entity<CMAutomatedVendorComponent> vendor, CMVendorSection section, CMVendorEntry entry)
    {
        if (!IsRequisitionEntry(section, entry) || entry.Amount is <= 0 ||
            !_prototypes.TryIndex(entry.Id, out var prototype) ||
            !prototype.HasComponent<ItemComponent>(_compFactory) ||
            prototype.HasComponent<CMVendorBundleComponent>(_compFactory) ||
            prototype.HasComponent<CMVendorMapToSquadComponent>(_compFactory) ||
            prototype.HasComponent<CMChangeUserOnVendComponent>(_compFactory))
            return 0;

        var amount = entry.Amount ?? int.MaxValue;
        // A partly filled last stack cannot satisfy an order for a complete product.
        if (entry.Amount != null && prototype.TryGetComponent<StackComponent>(out var stack, _compFactory) &&
            vendor.Comp.PartialProductStacks.GetValueOrDefault(stack.StackTypeId) > 0)
            amount--;
        return Math.Max(0, amount);
    }

    public bool TryIssueRequisition(Entity<CMAutomatedVendorComponent> vendor, EntityUid actor, int section, int entry)
    {
        if (_net.IsClient || !_accessReader.IsAllowed(actor, vendor) || !_rmcPower.IsPowered(vendor) ||
            section < 0 || section >= vendor.Comp.Sections.Count ||
            entry < 0 || entry >= vendor.Comp.Sections[section].Entries.Count)
            return false;

        var selected = vendor.Comp.Sections[section].Entries[entry];
        if (GetRequisitionAmount(vendor, vendor.Comp.Sections[section], selected) <= 0)
            return false;

        var attempt = new ActivatableUIOpenAttemptEvent(actor);
        RaiseLocalEvent(vendor.Owner, attempt);
        if (attempt.Cancelled)
            return false;

        var message = new CMVendorVendBuiMsg(section, entry, new()) { Actor = actor };
        VendFromMenu(vendor, ref message, autoGrab: false);
        return true;
    }

}
