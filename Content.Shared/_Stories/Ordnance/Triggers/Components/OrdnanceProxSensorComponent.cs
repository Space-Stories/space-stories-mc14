using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance.Triggers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class OrdnanceProxSensorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField]
    public EntityUid? Primer;

    [DataField, AutoNetworkedField]
    public float Range = 2f;

    [DataField, AutoNetworkedField]
    public float Delay = 5f;

    [DataField, AutoNetworkedField]
    public float ArmingTime = 5f;

    [DataField, AutoNetworkedField]
    public float ArmingTimeRemaining;

    [DataField, AutoNetworkedField]
    public float TriggerDelayRemaining;
}

[Serializable, NetSerializable]
public enum OrdnanceProxSensorUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class OrdnanceProxSensorConfigMessage : BoundUserInterfaceMessage
{
    public float ArmTime;
    public float Range;
    public float Delay;

    public OrdnanceProxSensorConfigMessage(float armTime, float range, float delay)
    {
        ArmTime = armTime;
        Range = range;
        Delay = delay;
    }
}
