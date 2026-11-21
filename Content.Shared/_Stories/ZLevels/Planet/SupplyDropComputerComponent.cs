using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.SupplyDrop;

public sealed partial class SupplyDropComputerComponent
{
    [DataField, AutoNetworkedField]
    public int TargetZ;
}
