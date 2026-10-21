using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.MilitaryPolice;

/// <summary>
/// Personnel and investigation data replicated only to users of the console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class RMCPoliceUiComponent : Component
{
    public override bool SessionSpecific => true;

    [AutoNetworkedField]
    public RMCPoliceUiData? Data;
}
