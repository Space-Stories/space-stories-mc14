using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Granted while the armor is worn - keys slow get-up, tackle resistance, and the drag slowdown (SlowOnPull entry in base_xeno.yml) to actually wearing it, not just having the Juggernaut skill.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(JuggernautGetUpSystem), typeof(JuggernautTackleResistSystem))]
public sealed partial class JuggernautArmorWornComponent : Component
{
    [DataField]
    public TimeSpan GetUpDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public int TackleResistMin = 8;
}
