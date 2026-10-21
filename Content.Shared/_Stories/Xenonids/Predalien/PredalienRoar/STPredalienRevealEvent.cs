namespace Content.Shared._Stories.Xenonids.Predalien.PredalienRoar;

public sealed class STPredalienRevealEvent : EntityEventArgs
{
    public readonly EntityUid Target;
    public readonly TimeSpan RecloakPenalty;

    public STPredalienRevealEvent(EntityUid target, TimeSpan recloakPenalty)
    {
        Target = target;
        RecloakPenalty = recloakPenalty;
    }
}
