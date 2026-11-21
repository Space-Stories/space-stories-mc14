using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Doors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(TurnstilePushSystem))]
public sealed partial class TurnstilePushedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Turnstile;

    [DataField, AutoNetworkedField]
    public Vector2 TargetWorldPosition;

    [DataField, AutoNetworkedField]
    public float Speed = 5f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan EndTime;
}
