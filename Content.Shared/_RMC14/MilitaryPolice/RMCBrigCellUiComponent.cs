using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.MilitaryPolice;

/// <summary>
/// Sentence details replicated only to users of the cell controller.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class RMCBrigCellUiComponent : Component
{
    public override bool SessionSpecific => true;

    [AutoNetworkedField]
    public RMCBrigCellUiData? Data;
}
