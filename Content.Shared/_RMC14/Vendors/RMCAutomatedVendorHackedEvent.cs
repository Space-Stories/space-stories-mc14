namespace Content.Shared._RMC14.Vendors;

/// <summary>
/// Raised after a user successfully removes a vendor's access restrictions.
/// </summary>
[ByRefEvent]
public readonly record struct RMCAutomatedVendorHackedEvent(EntityUid User);
