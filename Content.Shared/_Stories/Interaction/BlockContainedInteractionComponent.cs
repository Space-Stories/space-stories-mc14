using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Interaction;

[RegisterComponent, NetworkedComponent, Access(typeof(BlockContainedInteractionSystem))]
public sealed partial class BlockContainedInteractionComponent : Component
{
}
