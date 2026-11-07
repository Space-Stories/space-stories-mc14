using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Traps;

[RegisterComponent, NetworkedComponent]
[Access(typeof(HunterTrapSystem))]
public sealed partial class HunterSnareProjectileComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Deployed;

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan DazeTime = TimeSpan.FromSeconds(6);

    [DataField]
    public float PushMaxRange = 2f;
}
