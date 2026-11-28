using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STSynthSpecializationComponent : Component
{
    [DataField, AutoNetworkedField]
    public STSynthSpecialization Specialization = STSynthSpecialization.General;
}

[Serializable, NetSerializable]
public enum STSynthSpecialization
{
    General,
    Command,
    Engineering,
    Medical,
    Requisitions,
    MilitaryPolice,
    Aviation,
    Intel,
}
