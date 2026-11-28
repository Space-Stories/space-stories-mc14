using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Doors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(TurnstilePushSystem))]
public sealed partial class TurnstilePushComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Id;

    [DataField, AutoNetworkedField]
    public float PushDistance = 1f;

    [DataField, AutoNetworkedField]
    public float PushSpeed = 5f;
}
