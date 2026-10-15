using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._Stories.Sponsors;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Word;
using Content.Shared._Stories.Hunter.Bracer;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared._RMC14.Language.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stories.TTS;

public sealed partial class TTSSystem : EntitySystem
{
    private const int MaxMessageChars = 100 * 2;
    [Dependency] private readonly BracerSystem _bracer = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SponsorsManager _sponsorsManager = default!;
    [Dependency] private readonly SharedLanguageSystem _language = default!;

    private readonly List<string> _sampleText =
        new()
        {
            "Внимание, морпехи, заряжайте оружие и готовьтесь к бою. Обнаружена активность ксеноморфов.",
            "Коммандир, у нас тут настоящая бойня! Запрашиваем срочную эвакуацию с Алмаера!",
            "Ш-ш-ш... Чужие... близко... Я чувствую их запах.",
        };

    [Dependency] private readonly TtsAudioProcessingSystem _ttsAudio = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    private bool _isEnabled;

    public override void Initialize()
    {
        _cfg.OnValueChanged(SCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke, new[] { typeof(HeadsetSystem) });
        SubscribeLocalEvent<TTSPlaybackModifierComponent, GetTTSPlaybackModifiersEvent>(OnGetTTSPlaybackModifiers);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        SubscribeLocalEvent<XenoWordQueenSpokenEvent>(OnXenoWordQueenSpoken);
        SubscribeLocalEvent<OverwatchConsoleMessageSentEvent>(OnOverwatchMessageSent);
        SubscribeLocalEvent<OverwatchConsoleObjectiveSetEvent>(OnOverwatchObjectiveSet);

        InitializeSanitize();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        if (ev.IsHunter)
            soundData = await _ttsAudio.ApplyPlaybackEffects(soundData, TTSAudioEffect.Hunter);

        RaiseNetworkEvent(new PlayTTSEvent(soundData, previewText), Filter.SinglePlayer(args.SenderSession));
    }

    private void OnXenoWordQueenSpoken(XenoWordQueenSpokenEvent ev)
    {
        if (TryComp<TTSComponent>(ev.Source, out var ttsComp) && !string.IsNullOrEmpty(ttsComp.VoicePrototypeId))
        {
            PlayGlobalTTS(
                ev.Message,
                ttsComp.VoicePrototypeId,
                ev.Filter,
                TTSAudioEffect.XenoHivemind);
        }
    }

    private void OnOverwatchMessageSent(OverwatchConsoleMessageSentEvent ev)
    {
        var voice = _cfg.GetCVar(SCCVars.TTSAresVoice);
        if (TryComp<TTSComponent>(ev.Actor, out var tts) && !string.IsNullOrEmpty(tts.VoicePrototypeId))
            voice = tts.VoicePrototypeId;

        if (!string.IsNullOrEmpty(voice))
        {
            var combinedFilter = Filter.Empty().AddPlayers(ev.SquadFilter.Recipients).AddPlayers(ev.ConsoleFilter.Recipients);

            if (TryComp<ActorComponent>(ev.Actor, out var actor))
                combinedFilter.RemovePlayer(actor.PlayerSession);

            PlayGlobalTTS(
                ev.Message,
                voice,
                combinedFilter,
                isRadio: true);
        }
    }

    private void OnOverwatchObjectiveSet(OverwatchConsoleObjectiveSetEvent ev)
    {
        var voice = _cfg.GetCVar(SCCVars.TTSAresVoice);
        if (TryComp<TTSComponent>(ev.Actor, out var tts) && !string.IsNullOrEmpty(tts.VoicePrototypeId))
            voice = tts.VoicePrototypeId;

        if (!string.IsNullOrEmpty(voice))
        {
            var combinedFilter = Filter.Empty().AddPlayers(ev.SquadFilter.Recipients).AddPlayers(ev.ConsoleFilter.Recipients);

            if (TryComp<ActorComponent>(ev.Actor, out var actor))
                combinedFilter.RemovePlayer(actor.PlayerSession);

            PlayGlobalTTS(
                ev.Message,
                voice,
                combinedFilter,
                isRadio: true);
        }
    }

    private bool ValidateVoiceForEntity(EntityUid uid, TTSVoicePrototype voice)
    {
        if (voice.Sex != Sex.Unsexed)
        {
            if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            {
                if (humanoid.Sex != voice.Sex)
                    return false;
            }
        }

        if (voice.Blacklist != null && voice.Blacklist.Count > 0)
        {
            if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            {
                if (voice.Blacklist.Contains(humanoid.Species))
                    return false;
            }
        }

        if (voice.SponsorOnly)
        {
            NetUserId? userId = null;
            if (TryComp<ActorComponent>(uid, out var actor))
                userId = actor.PlayerSession.UserId;
            else if (TryComp<MindContainerComponent>(uid, out var mindContainer) && TryComp<MindComponent>(mindContainer.Mind, out var mind))
                userId = mind.UserId;

            if (userId == null)
                return false;

            if (_sponsorsManager.TryGetInfo(userId.Value, out var sponsorInfo))
            {
                if (sponsorInfo.AllowedTTSVoices == null || !sponsorInfo.AllowedTTSVoices.Contains(voice.ID))
                    return false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private bool GetVoicePrototype(string voiceId, [NotNullWhen(true)] out TTSVoicePrototype? voicePrototype)
    {
        if (!_prototypeManager.TryIndex(voiceId, out voicePrototype))
            return _prototypeManager.TryIndex("father_grigori", out voicePrototype);

        return true;
    }

    private bool TryResolveVoiceForEntity(
        EntityUid uid,
        string? voiceId,
        [NotNullWhen(true)] out TTSVoicePrototype? voicePrototype)
    {
        voicePrototype = null;
        if (voiceId == null)
            return false;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);

        if (GetVoicePrototype(voiceEv.VoiceId, out voicePrototype) && ValidateVoiceForEntity(uid, voicePrototype))
            return true;

        return GetVoicePrototype("father_grigori", out voicePrototype);
    }

    private void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        if (args.Message.Length > MaxMessageChars ||
            !TryResolveVoiceForEntity(uid, component.VoicePrototypeId, out var protoVoice))
            return;

        var messageToUse = args.Message;

        if (HasComp<HunterComponent>(uid) && _bracer.IsHunterWithBracer(uid, out var bracer) &&
            bracer.Value.Comp.TranslatorActive)
            messageToUse = args.OriginalMessage;

        if (messageToUse.Contains('\u200B'))
            return;

        bool isRadio = args.Channel != null;

        if (args.ObfuscatedMessage != null)
        {
            var playbackModifiers = GetPlaybackModifiers(uid, ChatSystem.WhisperClearRange);
            HandleWhisper(uid, messageToUse, protoVoice.Speaker, isRadio, args.Language, playbackModifiers);
            return;
        }

        HandleSay(
            uid,
            messageToUse,
            protoVoice.Speaker,
            isRadio,
            args.Language,
            GetPlaybackModifiers(uid, ChatSystem.VoiceRange),
            args.Channel?.ID);
    }

    private void OnGetTTSPlaybackModifiers(Entity<TTSPlaybackModifierComponent> ent, ref GetTTSPlaybackModifiersEvent args)
    {
        args.AddVolumeMultiplier(ent.Comp.VolumeMultiplier);
        args.AddRangeMultiplier(ent.Comp.RangeMultiplier);
        args.AddAudioEffect(ent.Comp.AudioEffects);

        if (ent.Comp.MaxDistance != null)
            args.SetMaxDistance(ent.Comp.MaxDistance.Value);

        if (ent.Comp.ReferenceDistance != null)
            args.SetReferenceDistance(ent.Comp.ReferenceDistance.Value);

        if (ent.Comp.RolloffFactor != null)
            args.SetRolloffFactor(ent.Comp.RolloffFactor.Value);
    }

    private TTSPlaybackModifiers GetPlaybackModifiers(EntityUid uid, float baseRange)
    {
        var ev = new GetTTSPlaybackModifiersEvent(baseRange);
        RaiseLocalEvent(uid, ev);

        return new TTSPlaybackModifiers(
            ev.HasVolumeOverride ? ev.VolumeMultiplier : 1f,
            ev.HasDistanceOverride ? ev.EffectiveMaxDistance : (float?) null,
            ev.HasSpatialOverride ? ev.ReferenceDistance : null,
            ev.HasSpatialOverride ? ev.RolloffFactor : null,
            ev.HasAudioEffects ? ev.AudioEffects : TTSAudioEffect.None);
    }

    private async void HandleSay(
        EntityUid uid,
        string message,
        string speaker,
        bool isRadio,
        ProtoId<LanguagePrototype> language,
        TTSPlaybackModifiers playbackModifiers,
        string? channelId = null)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null)
            return;

        soundData = await ProcessTtsAudio(uid, soundData, playbackModifiers.AudioEffects);

        var ttsEvent = new PlayTTSEvent(
            soundData,
            message,
            GetNetEntity(uid),
            isRadio: isRadio,
            radioChannel: channelId,
            volumeMultiplier: playbackModifiers.VolumeMultiplier,
            maxDistanceOverride: playbackModifiers.MaxDistanceOverride,
            referenceDistanceOverride: playbackModifiers.ReferenceDistanceOverride,
            rolloffFactorOverride: playbackModifiers.RolloffFactorOverride);

        FilterAndSend(
            uid,
            ttsEvent,
            playbackModifiers.MaxDistanceOverride ?? ChatSystem.VoiceRange,
            language);
    }

    private async void HandleWhisper(
        EntityUid uid,
        string message,
        string speaker,
        bool isRadio,
        ProtoId<LanguagePrototype> language,
        TTSPlaybackModifiers playbackModifiers)
    {
        var fullSoundData = await GenerateTTS(message, speaker, true);
        if (fullSoundData is null)
            return;

        fullSoundData = await ProcessTtsAudio(uid, fullSoundData, playbackModifiers.AudioEffects);

        var fullTtsEvent = new PlayTTSEvent(
            fullSoundData,
            message,
            GetNetEntity(uid),
            true,
            isRadio: isRadio,
            volumeMultiplier: playbackModifiers.VolumeMultiplier,
            maxDistanceOverride: playbackModifiers.MaxDistanceOverride,
            referenceDistanceOverride: playbackModifiers.ReferenceDistanceOverride,
            rolloffFactorOverride: playbackModifiers.RolloffFactorOverride);

        FilterAndSend(
            uid,
            fullTtsEvent,
            playbackModifiers.MaxDistanceOverride ?? ChatSystem.WhisperClearRange,
            language);
    }

    public async void PlayGlobalTTS(
        string text,
        string voiceId,
        Filter filter,
        TTSAudioEffect audioEffects = TTSAudioEffect.None,
        bool isAnnounce = false,
        bool isRadio = false)
    {
        if (text.Contains('\u200B')) return;

        if (!GetVoicePrototype(voiceId, out var protoVoice))
            return;

        var soundData = await GenerateTTS(text, protoVoice.Speaker);
        if (soundData == null) return;

        if (isRadio)
            audioEffects |= TTSAudioEffect.StandardRadio;

        soundData = await _ttsAudio.ApplyPlaybackEffects(soundData, audioEffects);

        var ev = new PlayTTSEvent(
            soundData,
            text,
            isRadio: isRadio,
            isAnnounce: isAnnounce);
        RaiseNetworkEvent(ev, filter);
    }

    private TTSAudioEffect GetSpecificVoiceEffects(EntityUid uid)
    {
        var effects = TTSAudioEffect.None;

        if (HasComp<HunterComponent>(uid))
            effects |= TTSAudioEffect.Hunter;

        if (HasComp<XenoComponent>(uid))
            effects |= TTSAudioEffect.XenoHivemind;

        return effects;
    }

    private async Task<byte[]> ProcessTtsAudio(EntityUid uid, byte[] data, TTSAudioEffect playbackEffects)
    {
        var effects = GetSpecificVoiceEffects(uid) | playbackEffects;
        return await _ttsAudio.ApplyPlaybackEffects(data, effects);
    }

    private void FilterAndSend(EntityUid source, PlayTTSEvent ev, float range, ProtoId<LanguagePrototype> language)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = xformQuery.GetComponent(source);
        var sourceMapCoords = _xforms.GetMapCoordinates(sourceXform);

        var isHunter = HasComp<HunterComponent>(source);
        var isMarine = HasComp<MarineComponent>(source);
        var isXeno = HasComp<XenoComponent>(source);

        var isTranslated = false;
        if (isHunter && _bracer.IsHunterWithBracer(source, out var bracer))
            isTranslated = bracer.Value.Comp.TranslatorActive;

        var isImaginaryFriend = TryComp<ImaginaryFriendComponent>(source, out var imaginaryFriend);

        var recipients = new List<ICommonSession>();
        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { } listener)
                continue;

            var listenerXform = xformQuery.GetComponent(listener);
            var listenerMapCoords = _xforms.GetMapCoordinates(listenerXform);

            if (listenerMapCoords.MapId != sourceMapCoords.MapId)
                continue;

            if (HasComp<GhostComponent>(listener))
            {
                if (HasComp<GhostHearingComponent>(listener) || listenerMapCoords.InRange(sourceMapCoords, range))
                    recipients.Add(player);
                continue;
            }

            if (!listenerMapCoords.InRange(sourceMapCoords, range))
                continue;

            if (!_language.CanUnderstand(listener, language))
                continue;

            if (isImaginaryFriend)
            {
                if (listener == source || listener == imaginaryFriend!.Imaginer)
                {
                    recipients.Add(player);
                    continue;
                }
                continue;
            }

            if (isXeno)
            {
                if (HasComp<XenoComponent>(listener))
                {
                    recipients.Add(player);
                    continue;
                }

                if (HasComp<HunterComponent>(listener))
                    recipients.Add(player);
            }
            else if (isMarine)
            {
                if (HasComp<XenoComponent>(listener))
                    continue;

                recipients.Add(player);
            }
            else if (isHunter)
            {
                if (isTranslated)
                    recipients.Add(player);
                else
                {
                    if (HasComp<HunterComponent>(listener))
                        recipients.Add(player);
                }
            }
            else
                recipients.Add(player);
        }

        if (recipients.Count > 0)
            RaiseNetworkEvent(ev, Filter.Empty().AddPlayers(recipients));
    }

    public async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        if (!_isEnabled)
            return null;

        var textSanitized = Sanitize(text);
        if (textSanitized == "")
            return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}

public readonly record struct TTSPlaybackModifiers(
    float VolumeMultiplier,
    float? MaxDistanceOverride,
    float? ReferenceDistanceOverride,
    float? RolloffFactorOverride,
    TTSAudioEffect AudioEffects);

public sealed class TransformSpeakerVoiceEvent : EntityEventArgs
{
    public EntityUid Sender;
    public string VoiceId;

    public TransformSpeakerVoiceEvent(EntityUid sender, string voiceId)
    {
        Sender = sender;
        VoiceId = voiceId;
    }
}
