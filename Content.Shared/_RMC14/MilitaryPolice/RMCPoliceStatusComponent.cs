using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.MilitaryPolice;

/// <summary>
/// Status resolved from the person's linked record, with the worn ID as a fallback for legacy records.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCPoliceStatusComponent : Component
{
    [DataField, AutoNetworkedField]
    public RMCPoliceStatus Status;
}
