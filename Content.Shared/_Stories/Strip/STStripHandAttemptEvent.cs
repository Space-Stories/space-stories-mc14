namespace Content.Shared._Stories.Strip;

[ByRefEvent]
public record struct STStripHandAttemptEvent(EntityUid User, EntityUid Target, EntityUid Item, bool Insert)
{
    public bool Cancelled;
    public LocId? Reason;
}
