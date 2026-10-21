using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Humanoid.Markings;

[RegisterComponent, NetworkedComponent]
public sealed partial class STIntentsEyeColorComponent : Component
{
    [DataField]
    public Color EyeColorHelp = Color.FromHex("#00ff00");

    [DataField]
    public Color EyeColorDisarm = Color.FromHex("#5a5afd");

    [DataField]
    public Color EyeColorGrab = Color.FromHex("#efa700");

    [DataField]
    public Color EyeColorHarm = Color.FromHex("#ff0000");

    [DataField]
    public Color DeadEyeColor = Color.FromHex("#000000");
}
