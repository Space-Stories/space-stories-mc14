using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.Banish;

[RegisterComponent]
[Access(typeof(STXenoBanishSystem))]
public sealed partial class STHiveBanishmentsComponent : Component
{
    [ViewVariables]
    public Dictionary<NetUserId, STBanishmentEntry> Banished = new();

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(30);

    [DataField]
    public FixedPoint2 BanishPlasmaCost = 500;

    [DataField]
    public FixedPoint2 ReadmitPlasmaCost = 100;

    [DataField]
    public EntProtoId BanishedHiveId = "STXenoHiveBanished";
}

public sealed class STBanishmentEntry(string name, TimeSpan expiresAt, EntityUid? xeno)
{
    public string Name = name;
    public TimeSpan ExpiresAt = expiresAt;
    public EntityUid? Xeno = xeno;
}
