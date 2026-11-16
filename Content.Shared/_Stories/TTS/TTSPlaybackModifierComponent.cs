namespace Content.Shared._Stories.TTS;

/// <summary>
/// Applies TTS playback modifiers and server-side audio effects to speech from this entity.
/// This is the reusable component-backed provider for the shared TTS modifier event; other systems
/// can contribute the same kinds of modifiers without using this component directly.
/// </summary>
[RegisterComponent]
// ReSharper disable once InconsistentNaming
public sealed partial class TTSPlaybackModifierComponent : Component
{
    /// <summary>
    /// Client playback gain multiplier applied on top of the listener's TTS volume setting.
    /// It is capped at <see cref="GetTTSPlaybackModifiersEvent.MaxClientVolumeMultiplier"/>
    /// to avoid saturating the OpenAL source. Use <see cref="AudioEffects"/> for loudness processing.
    /// </summary>
    [DataField("volumeMultiplier")]
    public float VolumeMultiplier = 1f;

    /// <summary>
    /// Multiplier applied to the server send range and client playback max distance.
    /// Multiple range multipliers from different modifier providers are multiplied together.
    /// </summary>
    [DataField("rangeMultiplier")]
    public float RangeMultiplier = 1f;

    /// <summary>
    /// Explicit max distance override. When set, this takes precedence over <see cref="RangeMultiplier"/>.
    /// Multiple explicit max distances use the largest value.
    /// </summary>
    [DataField("maxDistance")]
    public float? MaxDistance;

    /// <summary>
    /// Distance at which positional TTS starts to attenuate. Multiple values use the largest value.
    /// </summary>
    [DataField("referenceDistance")]
    public float? ReferenceDistance;

    /// <summary>
    /// Strength of positional attenuation. Multiple values use the smallest value.
    /// </summary>
    [DataField("rolloffFactor")]
    public float? RolloffFactor;

    /// <summary>
    /// Server-side audio effects applied to generated TTS before it is sent to clients.
    /// Effects from different modifier providers are combined into one processing chain.
    /// </summary>
    [DataField("audioEffects")]
    public TTSAudioEffect AudioEffects = TTSAudioEffect.None;
}
