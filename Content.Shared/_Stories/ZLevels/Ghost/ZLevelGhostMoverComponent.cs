using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.ZLevels.Ghost;

/// <summary>
/// Component that allows ghosts to move between Z levels via Ghost UI buttons.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelGhostMoverComponent : Component;

[Serializable, NetSerializable]
public sealed class ZLevelGhostMoveMessage(bool up) : EntityEventArgs
{
    public bool Up { get; } = up;
}
