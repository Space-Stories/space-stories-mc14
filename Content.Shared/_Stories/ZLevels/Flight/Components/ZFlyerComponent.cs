using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.ZLevels.Flight.Components;

/// <summary>
/// A basic component that allows entities to fly between z-levels.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), Access(typeof(SharedZFlightSystem))]
public sealed partial class ZFlyerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public int TargetMapHeight = 0;

    [DataField]
    public float FlightSpeed = 1.5f;

    [DataField]
    public float DefaultGravityIntensity = 1f;

    /// <summary>
    /// Spawned on client only every tick in flight state
    /// </summary>
    [DataField]
    public EntProtoId? FlightVfx;

    [DataField]
    public TimeSpan NextVfx = TimeSpan.Zero;
}
