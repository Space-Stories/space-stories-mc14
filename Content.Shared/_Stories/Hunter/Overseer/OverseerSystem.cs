namespace Content.Shared._Stories.Overseer;

public sealed class OverseerSystem : EntitySystem
{
    [Dependency]
    private readonly MetaDataSystem _metaData = default!;

    public Entity<OverseerComponent> EnsureOverseer()
    {
        var query = EntityQueryEnumerator<OverseerComponent>();
        if (query.MoveNext(out var uid, out var comp))
            return (uid, comp);

        var newUid = Spawn();
        _metaData.SetEntityName(newUid, Loc.GetString("st-hunter-overseer-announcer"));
        var newComp = EnsureComp<OverseerComponent>(newUid);
        return (newUid, newComp);
    }
}
