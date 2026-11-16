using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance.Triggers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class OrdnanceTimerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public EntityUid? Primer;

    [DataField, AutoNetworkedField]
    public float TimeRemaining = 5f;

    [DataField, AutoNetworkedField]
    public float SelectedTime = 5f;
}

[Serializable, NetSerializable]
public enum OrdnanceTimerUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class OrdnanceTimerSetMessage : BoundUserInterfaceMessage
{
    public float Time;

    public OrdnanceTimerSetMessage(float time)
    {
        Time = time;
    }
}
