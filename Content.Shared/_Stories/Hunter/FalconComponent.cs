using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared._Stories.Hunter;
using Robust.Shared.Audio;

namespace Content.Shared.Hunter.FalconComponent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FalconDroneComponent : Component
{

    [DataField, AutoNetworkedField]
    public EntityUid? Hunter;

    [DataField]
    public EntProtoId DestroyedPrototype = "STDeviceFalconDroneDestroyed";

    [DataField]
    public EntProtoId ControlActionId = "STActionControlFalconDrone";

    [DataField]
    public EntProtoId RecallActionId = "STActionRecallFalconDrone";

    [ViewVariables]
    public EntityUid? ControlAction;

    [ViewVariables]
    public EntityUid? RecallAction;

    [DataField, AutoNetworkedField]
    public EntityUid Controller;

    [DataField, AutoNetworkedField]
    public EntityUid? PreviousEyeTarget;
}

[RegisterComponent]
public sealed partial class FalconSourceBracerComponent : Component
{
    [DataField]
    public EntityUid? Controller;
}


