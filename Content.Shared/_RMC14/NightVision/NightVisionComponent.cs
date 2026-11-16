using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.NightVision;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class NightVisionComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype>? Alert;

    [DataField, AutoNetworkedField]
    public NightVisionState State = NightVisionState.Full;

    [DataField, AutoNetworkedField]
    public bool Overlay;

    [DataField, AutoNetworkedField]
    public bool Innate;

    [DataField, AutoNetworkedField]
    public bool SeeThroughContainers;

    [DataField, AutoNetworkedField]
    public bool Green;

    [DataField, AutoNetworkedField]
    public bool Mesons;

    [DataField, AutoNetworkedField]
    public bool BlockScopes;

    [DataField, AutoNetworkedField]
    public bool OnlyHalf;

    // Stories-MesonFix-Start
    [DataField, AutoNetworkedField]
    public NightVisionState? STInnateState;
    // Stories-MesonFix-End
}

[Serializable, NetSerializable]
public enum NightVisionState
{
    Off,
    Half,
    Full,
}
