using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.MilitaryPolice;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class RMCPoliceClueVisibilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Revealed;
}
