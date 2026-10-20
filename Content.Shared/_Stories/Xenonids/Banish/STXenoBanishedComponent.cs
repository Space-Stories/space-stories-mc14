using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Xenonids.Banish;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(STXenoBanishSystem))]
public sealed partial class STXenoBanishedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? OriginalHive;

    [DataField, AutoNetworkedField]
    public EntityUid? BanishedHive;

    [DataField, AutoNetworkedField]
    public TimeSpan DamageEvery = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextDamageAt;

    [DataField, AutoNetworkedField]
    public float DamageDivisor = 23;

    [DataField, AutoNetworkedField]
    public ProtoId<DamageTypePrototype> DamageType = "Blunt";

    [DataField, AutoNetworkedField]
    public int Deliveries;

    [DataField]
    public HashSet<EntityUid> DeliveredHosts = new();

    [DataField, AutoNetworkedField]
    public TimeSpan DeliveryTimeout = TimeSpan.FromMinutes(5);
}
