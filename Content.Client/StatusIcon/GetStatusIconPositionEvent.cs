// RMC14 - Coordinates status icon placement with the marine HUD.
using System.Numerics;
using Content.Shared.StatusIcon;

namespace Content.Client.StatusIcon;

/// <summary>
/// Lets another HUD overlay move a status icon below the space it occupies on this entity.
/// Positions and sizes use the entity's local world coordinates.
/// </summary>
[ByRefEvent]
public record struct GetStatusIconPositionEvent(StatusIconData Icon, Vector2 Position, Vector2 Size);
