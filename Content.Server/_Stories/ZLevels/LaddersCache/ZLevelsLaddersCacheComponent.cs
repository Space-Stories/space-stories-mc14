namespace Content.Server._Stories.ZLevels.LaddersCache;

/// <summary>
/// Cached positions of ladders entities (HighGround that act as ramps) on a given map grid.
/// Stored on the map/grid entity for quick spatial lookups by NPC navigation systems.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelsLaddersCacheComponent : Component
{
    /// <summary>
    /// All ramp-like slope positions on this map, keyed by tile index.
    /// Value contains the slope entity UID and the cardinal direction it faces (the "uphill" direction).
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector2i, CachedSlope> Slopes = new();
}

/// <summary>
/// Cached data about a single slope.
/// </summary>
public struct CachedSlope
{
    /// <summary>
    /// The slope entity UID.
    /// </summary>
    public EntityUid Entity;

    /// <summary>
    /// The cardinal direction the slope faces — i.e. the direction you walk to ascend.
    /// </summary>
    public Direction Direction;
}
