using Robust.Shared.Serialization;

namespace Content.Shared._Stories.TTS;

[Flags]
[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
public enum TTSAudioEffect : byte
{
    None = 0,
    StandardRadio = 1 << 0,
    Megaphone = 1 << 1,
    Ares = 1 << 2,
    XenoHivemind = 1 << 3,
    Hunter = 1 << 4,
}
