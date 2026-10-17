using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Lets a Squire hand a belt straight to a wielded M134C-JLCW instead of a solo reload - same idea as a Loader feeding a wielded M5ATL.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(JuggernautBeltReloadSystem))]
public sealed partial class JuggernautBeltAssistedReloadComponent : Component;
