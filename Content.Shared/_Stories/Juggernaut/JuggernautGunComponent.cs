using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Marks the M134C-JLCW itself - distinct from <see cref="JuggernautWhitelistComponent"/>, which marks the player.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(JuggernautBatterySystem), typeof(JuggernautAssistedRepairSystem))]
public sealed partial class JuggernautGunComponent : Component;
