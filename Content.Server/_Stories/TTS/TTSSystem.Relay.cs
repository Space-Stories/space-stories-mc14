using System.Threading.Tasks;
using Content.Shared._Stories.TTS;
using Robust.Shared.Player;

namespace Content.Server._Stories.TTS;

public sealed partial class TTSSystem
{
    public async Task PlayRelayedTTS(
        EntityUid speaker,
        string message,
        Filter recipients,
        EntityUid playbackSource,
        TTSAudioEffect audioEffects,
        bool isRadio)
    {
        if (message.Length > MaxMessageChars ||
            message.Contains('\u200B') ||
            !TryComp<TTSComponent>(speaker, out var tts) ||
            !TryResolveVoiceForEntity(speaker, tts.VoicePrototypeId, out var voice))
        {
            return;
        }

        var soundData = await GenerateTTS(message, voice.Speaker);
        if (soundData == null || !Exists(speaker) || !Exists(playbackSource))
            return;

        soundData = await ProcessTtsAudio(speaker, soundData, audioEffects);
        if (!Exists(speaker) || !Exists(playbackSource))
            return;

        var ev = new PlayTTSEvent(
            soundData,
            message,
            GetNetEntity(playbackSource),
            originalSourceUid: GetNetEntity(speaker),
            isRadio: isRadio);
        RaiseNetworkEvent(ev, recipients);
    }
}
