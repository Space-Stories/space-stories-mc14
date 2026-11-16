using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float>
        BaseFallingDamage = CVarDef.Create("stories.zlevels.base_falling_damage", 2.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        BaseFallingOtherDamage = CVarDef.Create("stories.zlevels.base_falling_other_damage", 0.4f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        BaseFallingStunTime = CVarDef.Create("stories.zlevels.base_falling_stun_time", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        BaseFallingOtherStunTime = CVarDef.Create("stories.zlevels.base_falling_other_stun_time", 0.06f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        MurkZScale = CVarDef.Create("stories.murk.z_scale", 5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> ZLevelsPhysicsTickRate =
        CVarDef.Create("stories.zlevels.physics.tick_rate", 60, CVar.ARCHIVE);

    public static readonly CVarDef<bool> ZLevelsPhysicsClientSimulation =
        CVarDef.Create("stories.zlevels.physics.client_simulation", true, CVar.ARCHIVE | CVar.CLIENT);

    /**
     * Physics
     */

    public static readonly CVarDef<float>
        ZLevelsPhysicsGravityForce = CVarDef.Create("stories.zlevels.physics.gravity_force", 9.8f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        ZLevelsPhysicsVelocityLimit = CVarDef.Create("stories.zlevels.physics.velocity_limit", 20f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    public static readonly CVarDef<float>
        ZLevelsPhysicsImpactVelocity = CVarDef.Create("stories.zlevels.physics.impact_velocity", 3f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Rendering
     */

    public static readonly CVarDef<int>
        ZLevelsRenderingMaxZLevelsBelowRendering = CVarDef.Create("stories.zlevels.rendering.max_zlevels_below_rendering", 1, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum number of Z-levels below the player to render on the client.
    /// </summary>
    public static readonly CVarDef<int>
        ZLevelsRenderingMaxLevels = CVarDef.Create("stories.zlevels.rendering.max_levels", 2, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    /// Whether to apply the blur shader to lower Z-levels.
    /// </summary>
    public static readonly CVarDef<bool>
        ZLevelsRenderingBlurShader = CVarDef.Create("stories.zlevels.rendering.blur_shader", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    /// Draws a lip under tile edges bordering an opening into the z-level below, giving floors visual thickness.
    /// </summary>
    public static readonly CVarDef<bool>
        ZLevelsRenderingTileVolume = CVarDef.Create("stories.zlevels.rendering.tile_volume", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    /**
     * Audio
     */

    /// <summary>
    /// How many decibels of volume are subtracted from a PVS-positioned sound for every Z-level
    /// it is away from the listener.
    /// </summary>
    public static readonly CVarDef<float>
        ZLevelsAudioPerLevelAttenuation = CVarDef.Create("stories.zlevels.audio.per_level_attenuation_db", 9f, CVar.ARCHIVE | CVar.CLIENT);

    /// <summary>
    /// Occlusion added to a cross-Z-level sound when an opaque tile blocks the floor/ceiling between
    /// the source and the listener, on top of the flat per-level attenuation.
    /// </summary>
    public static readonly CVarDef<float>
        ZLevelsAudioFloorOcclusion = CVarDef.Create("stories.zlevels.audio.floor_occlusion", 3f, CVar.ARCHIVE | CVar.CLIENT);
}
