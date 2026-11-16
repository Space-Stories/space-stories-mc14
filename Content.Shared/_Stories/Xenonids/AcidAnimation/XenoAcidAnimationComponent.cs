using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.AcidAnimation;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class XenoAcidAnimationComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public List<EntProtoId> ActionIds = new();

    [DataField]
    public float ToggleRateLimit = 0.25f;

    public TimeSpan NextToggleAt;
}
