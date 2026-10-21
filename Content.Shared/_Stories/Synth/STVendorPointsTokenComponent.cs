using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STVendorPointsTokenComponent : Component
{
    [DataField, AutoNetworkedField]
    public string PointsType = "ExperimentalTools";

    [DataField, AutoNetworkedField]
    public int Points = 45;
}
