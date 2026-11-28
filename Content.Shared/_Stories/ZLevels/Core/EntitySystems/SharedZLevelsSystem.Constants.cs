namespace Content.Shared._Stories.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem
{
    public static int MaxZLevelsBelowRendering = 5;
    public static int MaxZLevelsAboveRendering = 3;
    public const float ZLevelOffset = 0f;

    public const float ZGravityForce = 9.8f;
    private const float ZVelocityLimit = 20.0f;
    private const int MaxStepsPerFrame = 10;

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    private const float ImpactVelocityLimit = 3.5f;

    /// <summary>
    /// Distance to ground above which an entity is considered airborne (synced to BodyStatus.InAir).
    /// </summary>
    public const float AirborneHeightThreshold = 0.15f;
}
