using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Granted by the Squire training pamphlet - "is a Squire nearby" for the faster belt reloads.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SquireWhitelistComponent : Component;
