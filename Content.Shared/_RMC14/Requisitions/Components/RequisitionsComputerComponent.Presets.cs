using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Requisitions.Components;

public sealed partial class RequisitionsComputerComponent
{
    [DataField, AutoNetworkedField]
    public int MaxRequestLines = 50;
}
