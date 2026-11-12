using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Overwatch;

public sealed partial class OverwatchConsoleComponent
{
    [DataField, AutoNetworkedField]
    public int OrbitalZ;
}
