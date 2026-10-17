using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Armor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TackleResistComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1.0f;
}
