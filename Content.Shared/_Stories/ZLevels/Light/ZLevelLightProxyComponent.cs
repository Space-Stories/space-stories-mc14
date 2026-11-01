using Robust.Shared.GameStates;

namespace Content.Shared._Stories.ZLevels.Light;

/// <summary>
/// Marks a client-side proxy light entity that propagates illumination across adjacent Z-levels.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelLightProxyComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid SourceLight;
    [DataField, AutoNetworkedField] public int SourceDepth;
    [DataField, AutoNetworkedField] public int TargetDepth;
}
