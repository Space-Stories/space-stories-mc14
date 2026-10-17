using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Makes swapping the M134C-JLCW's ammo belt take a DoAfter instead of an instant insert. Faster with a Squire nearby, faster still handed off directly (JuggernautBeltAssistedReloadComponent).</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(JuggernautBeltReloadSystem))]
public sealed partial class JuggernautBeltReloadComponent : Component
{
    [DataField]
    public string Slot = "gun_magazine";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(9);

    [DataField]
    public TimeSpan SquireDelay = TimeSpan.FromSeconds(4.5);

    [DataField]
    public float SquireRange = 3f;

    [DataField]
    public TimeSpan AssistedDelay = TimeSpan.FromSeconds(3);

    /// <summary>Marks the DoAfter-confirmed insert so it isn't mistaken for a fresh player-initiated one and cancelled.</summary>
    public bool CompletingInsert;
}
