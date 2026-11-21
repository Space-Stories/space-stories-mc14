namespace Content.Shared._Stories.TTS;

// ReSharper disable once InconsistentNaming
public sealed class GetTTSPlaybackModifiersEvent(float baseRange) : EntityEventArgs
{
    private const float Epsilon = 0.0001f;

    /// <summary>
    /// Caps default TTS gain at 0.9, below the OpenAL source limit.
    /// </summary>
    public const float MaxClientVolumeMultiplier = 1.5f;

    public float BaseRange { get; } = MathF.Max(0f, baseRange);
    public float VolumeMultiplier { get; private set; } = 1f;
    public float RangeMultiplier { get; private set; } = 1f;
    public float? MaxDistance { get; private set; }
    public float? ReferenceDistance { get; private set; }
    public float? RolloffFactor { get; private set; }
    public TTSAudioEffect AudioEffects { get; private set; } = TTSAudioEffect.None;

    public bool HasVolumeOverride => MathF.Abs(VolumeMultiplier - 1f) > Epsilon;
    public bool HasDistanceOverride => MaxDistance != null || MathF.Abs(RangeMultiplier - 1f) > Epsilon;
    public bool HasSpatialOverride => ReferenceDistance != null || RolloffFactor != null;
    public bool HasAudioEffects => AudioEffects != TTSAudioEffect.None;
    public float EffectiveMaxDistance => MathF.Max(0f, MaxDistance ?? BaseRange * RangeMultiplier);

    public void AddVolumeMultiplier(float multiplier)
    {
        VolumeMultiplier = MathF.Min(
            MaxClientVolumeMultiplier,
            VolumeMultiplier * Math.Clamp(multiplier, 0f, MaxClientVolumeMultiplier));
    }

    public void AddRangeMultiplier(float multiplier)
    {
        RangeMultiplier *= MathF.Max(0f, multiplier);
    }

    public void SetMaxDistance(float maxDistance)
    {
        maxDistance = MathF.Max(0f, maxDistance);
        MaxDistance = MaxDistance == null ? maxDistance : MathF.Max(MaxDistance.Value, maxDistance);
    }

    public void SetReferenceDistance(float referenceDistance)
    {
        referenceDistance = MathF.Max(0f, referenceDistance);
        ReferenceDistance = ReferenceDistance == null
            ? referenceDistance
            : MathF.Max(ReferenceDistance.Value, referenceDistance);
    }

    public void SetRolloffFactor(float rolloffFactor)
    {
        rolloffFactor = MathF.Max(0f, rolloffFactor);
        RolloffFactor = RolloffFactor == null
            ? rolloffFactor
            : MathF.Min(RolloffFactor.Value, rolloffFactor);
    }

    public void AddAudioEffect(TTSAudioEffect effect)
    {
        AudioEffects |= effect;
    }
}
