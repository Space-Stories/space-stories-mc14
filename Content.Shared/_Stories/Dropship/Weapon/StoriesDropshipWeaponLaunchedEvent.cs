namespace Content.Shared._Stories.Dropship.Weapon;

/// <summary>
/// Raised on a dropship weapon after a successful launch has consumed its ammunition.
/// </summary>
[ByRefEvent]
public readonly record struct StoriesDropshipWeaponLaunchedEvent;
