using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._RMC14.Marines;

/// <summary>
/// A marine HUD draw command and the area it occupies, shared with other HUD overlays.
/// Bounds omit the transparent margins of fireteam markers.
/// </summary>
public readonly record struct MarineHudIcon(Texture Texture, Vector2 Position, Color? Modulate, Box2 Bounds);
