using Content.Shared._Stories.TTS; // Stories-TTS
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Megaphone;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCMegaphoneUserComponent : Component
{
    /// <summary>
    /// The sound played when the megaphone is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeechSoundsPrototype> MegaphoneSpeechSound = "RMCMegaphone";

    /// <summary>
    /// The verb used when the megaphone is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeechVerbPrototype> SpeechVerb = "Megaphone";

    /// <summary>
    /// The original verb used before the megaphone was used.
    /// Needed to restore the original verb when the megaphone is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeechVerbPrototype>? OriginalSpeechVerb;

    /// <summary>
    /// The original sounds used before the megaphone was used.
    /// Needed to restore the original sound when the megaphone is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeechSoundsPrototype>? OriginalSpeechSounds;

    /// <summary>
    /// The original suffix speech verbs used before the megaphone was used.
    /// Needed to restore the original verbs when the megaphone is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, ProtoId<SpeechVerbPrototype>>? OriginalSuffixSpeechVerbs;

    /// <summary>
    /// Override the default suffix speech verbs to use megaphone verbs.
    /// Allows to clearly record the options used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, ProtoId<SpeechVerbPrototype>> SuffixSpeechVerbs = new()
    {
        { "chat-speech-verb-suffix-exclamation-strong", "Megaphone" },
        { "chat-speech-verb-suffix-exclamation", "Megaphone" },
        { "chat-speech-verb-suffix-question", "Megaphone" },
        { "chat-speech-verb-suffix-stutter", "Megaphone" },
        { "chat-speech-verb-suffix-mumble", "Megaphone" },
    };

    // Stories-TTS-Start
    /// <summary>
    /// Stories: Multiplier applied to the base voice range when using a megaphone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float VoiceRangeMultiplier = 1.5f;

    /// <summary>
    /// Stories: Client playback gain multiplier. Values above 1.5 saturate the audio source.
    /// </summary>
    [DataField("ttsVolumeMultiplier"), AutoNetworkedField]
    public float TTSVolumeMultiplier = 1.5f;

    /// <summary>
    /// Stories: Multiplier applied to TTS range while using a megaphone.
    /// </summary>
    [DataField("ttsRangeMultiplier"), AutoNetworkedField]
    public float TTSRangeMultiplier = 1.5f;

    /// <summary>
    /// Stories: Keep the megaphone loud near its speaker before distance attenuation begins.
    /// </summary>
    [DataField("ttsReferenceDistance"), AutoNetworkedField]
    public float TTSReferenceDistance = 4f;

    /// <summary>
    /// Stories: Reduce positional attenuation so the megaphone remains intelligible at range.
    /// </summary>
    [DataField("ttsRolloffFactor"), AutoNetworkedField]
    public float TTSRolloffFactor = 0.25f;

    /// <summary>
    /// Stories: Server-side audio effects applied to TTS generated while using a megaphone.
    /// </summary>
    [DataField("ttsAudioEffects"), AutoNetworkedField]
    public TTSAudioEffect AudioEffects = TTSAudioEffect.Megaphone;
    // Stories-TTS-End
}
