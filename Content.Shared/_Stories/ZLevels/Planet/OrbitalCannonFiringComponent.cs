using Robust.Shared.GameStates;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.OrbitalCannon;

public sealed partial class OrbitalCannonFiringComponent
{
    [AutoNetworkedField]
    public MapCoordinates? TargetCoordinates;
}
