using Content.Shared.Actions;

namespace Content.Shared._Stories.Hunter.Falcon;

/// <summary>
/// Event fired when the hunter uses the drone control action from the action menu.
/// Triggers drone deployment or return depending on current state.
/// </summary>
public sealed partial class FalconControlActionEvent : InstantActionEvent
{
}
