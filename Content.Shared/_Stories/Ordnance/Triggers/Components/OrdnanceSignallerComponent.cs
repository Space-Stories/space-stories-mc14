using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance.Triggers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class OrdnanceSignallerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Frequency = 140.0f;

    [DataField, AutoNetworkedField]
    public int Code = 12;
}

[Serializable, NetSerializable]
public enum OrdnanceSignallerUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class OrdnanceSignallerUpdateMessage : BoundUserInterfaceMessage
{
    public float Frequency;
    public int Code;

    public OrdnanceSignallerUpdateMessage(float frequency, int code)
    {
        Frequency = frequency;
        Code = code;
    }
}

[Serializable, NetSerializable]
public sealed class OrdnanceSignallerTriggerMessage : BoundUserInterfaceMessage
{
}
