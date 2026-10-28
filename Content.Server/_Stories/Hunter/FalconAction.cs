using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Hunter.Falcon;

public sealed partial class ToggleFalconDroneEvent : InstantActionEvent
{
    [DataField, AutoNetworkedField]
    public EntProtoId Spawn = "STDeviceFalconDrone";

    [DataField, AutoNetworkedField]
    public EntityUid? Drone;
}
