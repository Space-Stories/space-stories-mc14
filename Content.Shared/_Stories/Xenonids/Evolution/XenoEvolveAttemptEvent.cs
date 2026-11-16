namespace Content.Shared._Stories.Xenonids.Evolution;

[ByRefEvent]
public record struct XenoEvolveAttemptEvent(bool DoPopup, bool Cancelled = false);
