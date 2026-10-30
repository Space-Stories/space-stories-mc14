using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Blood;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodSplatterEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField, AutoNetworkedField]
    public Angle Direction;

    [DataField, AutoNetworkedField]
    public int Variant;

    [DataField]
    public List<string> States = new() { "spray1", "spray2", "spray3" };

    [DataField]
    public float Distance = 0.5f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(0.5);
}
