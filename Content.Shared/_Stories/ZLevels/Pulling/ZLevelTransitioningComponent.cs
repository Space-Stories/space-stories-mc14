using Robust.Shared.GameStates;

namespace Content.Shared._Stories.ZLevels.Pulling;

/// <summary>
/// Added temporarily to entities (puller, pulled, carrier, carried) during Z-level movement or ladder transitions
/// to suppress dropping, stopping pull, or standing up.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelTransitioningComponent : Component
{
}
