using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public sealed class PlayTTSEvent : EntityEventArgs
{
    public byte[] Data { get; }
    public string Text { get; }
    public NetEntity? SourceUid { get; }
    public bool IsWhisper { get; }
    public NetEntity? OriginalSourceUid { get; }
    public float VolumeMultiplier { get; }
    public float? MaxDistanceOverride { get; }
    public float? ReferenceDistanceOverride { get; }
    public float? RolloffFactorOverride { get; }
    public bool IsRadio { get; }
    public string? RadioChannel { get; }
    public bool IsAnnounce { get; }

    public PlayTTSEvent(
        byte[] data,
        string text,
        NetEntity? sourceUid = null,
        bool isWhisper = false,
        NetEntity? originalSourceUid = null,
        bool isRadio = false,
        string? radioChannel = null,
        bool isAnnounce = false,
        float volumeMultiplier = 1f,
        float? maxDistanceOverride = null,
        float? referenceDistanceOverride = null,
        float? rolloffFactorOverride = null)
    {
        Data = data;
        Text = text;
        SourceUid = sourceUid;
        IsWhisper = isWhisper;
        OriginalSourceUid = originalSourceUid ?? sourceUid;
        VolumeMultiplier = volumeMultiplier;
        MaxDistanceOverride = maxDistanceOverride;
        ReferenceDistanceOverride = referenceDistanceOverride;
        RolloffFactorOverride = rolloffFactorOverride;
        IsRadio = isRadio;
        RadioChannel = radioChannel;
        IsAnnounce = isAnnounce;
    }
}
