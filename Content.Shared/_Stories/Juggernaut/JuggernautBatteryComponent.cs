namespace Content.Shared._Stories.Juggernaut;

/// <summary>A power cell locked to the M134C-JLCW - mirrors SmartGunSystem's hardcoded smartgun/battery lock.</summary>
[RegisterComponent]
[Access(typeof(JuggernautBatterySystem))]
public sealed partial class JuggernautBatteryComponent : Component;
