using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Rangefinder;

public sealed partial class RangefinderComponent
{
    [AutoNetworkedField]
    public int LastTargetZ;
}
