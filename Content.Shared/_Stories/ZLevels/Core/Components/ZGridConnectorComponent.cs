using Robust.Shared.GameStates;
namespace Content.Shared._Stories.ZLevels.Core.Components;

/// <summary>
/// When anchored, links this entity's parent grid to the grid on the z-level directly above,
/// provided a tile exists at this position on that upper grid.
/// Multiple connector entities can independently maintain the same grid pair.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZGridConnectorComponent : Component
{
}
