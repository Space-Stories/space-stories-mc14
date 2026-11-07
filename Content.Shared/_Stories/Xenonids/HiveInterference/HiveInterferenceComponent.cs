using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Xenonids.HiveInterference;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedHiveInterferenceSystem))]
public sealed partial class HiveInterferenceComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndAt;

    [DataField, AutoNetworkedField]
    public float MaxBuildup = 100f;

    [DataField, AutoNetworkedField]
    public bool Immune;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ImmuneUntil;

    [DataField]
    public TimeSpan ImmunityDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ImmunityExtension = TimeSpan.FromSeconds(1);

    [DataField]
    public ProtoId<AlertPrototype> Alert = "STHiveInterference";
}
