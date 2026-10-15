namespace Content.Shared._Stories.Xenonids.Predalien;

public sealed class STAbominationSpawnedEvent : EntityEventArgs
{
    public readonly EntityUid Predalien;

    public STAbominationSpawnedEvent(EntityUid predalien)
    {
        Predalien = predalien;
    }
}
