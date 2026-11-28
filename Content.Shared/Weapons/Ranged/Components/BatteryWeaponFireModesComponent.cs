using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Allows battery weapons to fire different types of projectiles
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(BatteryWeaponFireModesSystem))]
[AutoGenerateComponentState]
public sealed partial class BatteryWeaponFireModesComponent : Component
{
    /// <summary>
    /// A list of the different firing modes the weapon can switch between
    /// </summary>
    [DataField(required: true)]
    [AutoNetworkedField]
    public List<BatteryWeaponFireMode> FireModes = new();

    /// <summary>
    /// The currently selected firing mode
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public int CurrentFireMode;

    /// <summary> // Stories start
    /// The original BaseFireRate from RMCSelectiveFireComponent, saved when a fire mode with custom fireRate is selected.
    /// </summary>
    [DataField]
    [AutoNetworkedField] // Stories end
    public float OriginalBaseFireRate = 0f;

    /// <summary> // Stories start
    /// Whether a custom fire rate has been applied.
    /// </summary>
    [DataField]
    [AutoNetworkedField] // Stories End
    public bool HasCustomFireRate = false; // Stories
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class BatteryWeaponFireMode
{
    /// <summary>
    /// The projectile prototype associated with this firing mode
    /// </summary>
    [DataField("proto", required: true)]
    public EntProtoId Prototype = default!;

    /// <summary>
    /// The battery cost to fire the projectile associated with this firing mode
    /// </summary>
    [DataField]
    public float FireCost = 100;

    /// <summary> // Stories Start
    /// The fire rate to apply when this projectile type is selected. Overrides BaseFireRate on RMCSelectiveFireComponent.
    /// Set to 0 to leave BaseFireRate unchanged.
    /// </summary>
    [DataField]
    public float FireRate; // Stories end
}

[Serializable, NetSerializable]
public enum BatteryWeaponFireModeVisuals : byte
{
    State
}
