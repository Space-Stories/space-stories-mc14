using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Present while the get-up DoAfter runs - blocks other stand attempts (e.g. toggling rest) from skipping it.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(JuggernautGetUpSystem))]
public sealed partial class JuggernautGettingUpComponent : Component;
