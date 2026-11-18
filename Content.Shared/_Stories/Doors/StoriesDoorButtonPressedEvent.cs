using Content.Shared._RMC14.Doors;

namespace Content.Shared._Stories.Doors;

[ByRefEvent]
public readonly record struct StoriesDoorButtonPressedEvent(
    Entity<RMCDoorButtonComponent> Button,
    EntityUid User,
    string ButtonId);
