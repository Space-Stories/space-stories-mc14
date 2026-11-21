namespace Content.Shared._RMC14.Power;

/// <summary>
/// Raised on the APC after a user successfully removes its power cell.
/// </summary>
[ByRefEvent]
public readonly record struct RMCApcCellRemovedEvent(EntityUid User);

/// <summary>
/// Raised on the APC after a user successfully removes its electronics.
/// </summary>
[ByRefEvent]
public readonly record struct RMCApcElectronicsRemovedEvent(EntityUid User);

/// <summary>
/// Raised on the APC after a user cuts its main power wire.
/// </summary>
[ByRefEvent]
public readonly record struct RMCApcMainPowerWireCutEvent(EntityUid User);

/// <summary>
/// Raised on the APC when a user shorts its main power wire with a multitool.
/// </summary>
[ByRefEvent]
public readonly record struct RMCApcMainPowerWirePulsedEvent(EntityUid User);
