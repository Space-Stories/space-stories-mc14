namespace Content.Shared._Stories.Xenonids.Banish;

[RegisterComponent]
[Access(typeof(STXenoBanishSystem))]
public sealed partial class STBanishDeliveryComponent : Component
{
    [DataField]
    public EntityUid Banished;

    [DataField]
    public EntityUid Hive;

    [DataField]
    public TimeSpan ExpiresAt;
}
