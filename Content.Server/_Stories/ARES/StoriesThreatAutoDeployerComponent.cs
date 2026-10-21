using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Stories.ARES;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(StoriesThreatAutoDeployerSystem))]
public sealed partial class StoriesThreatAutoDeployerComponent : Component
{
    [DataField]
    public float Range = 7f;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan RetractDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public EntProtoId<IFFFactionComponent> FriendlyFaction = "FactionMarine";

    [DataField]
    public LocId? Announcement = "stories-ares-core-under-attack";

    [DataField]
    public SoundSpecifier? AnnouncementSound;

    [DataField]
    public TimeSpan AnnouncementCooldown = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCheck;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LastThreat;

    [DataField]
    public bool PendingAnnouncement;
}
