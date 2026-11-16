using Content.Server.Chat.Systems;
using Content.Server._Stories.TTS; // Stories-TTS
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Megaphone;
using Content.Shared._Stories.TTS; // Stories-TTS
using Content.Shared.Ghost; // Stories-TTS
using Content.Shared.Speech;
using Robust.Server.Console;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Server.Chat.Systems.ChatSystem; // Stories-TTS

namespace Content.Server._RMC14.Megaphone;

public sealed class RMCServerMegaphoneSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IServerConsoleHost _console = default!;
    // Stories-TTS-Start
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    // Stories-TTS-End

    public override void Initialize()
    {
        SubscribeLocalEvent<ActorComponent, MegaphoneInputEvent>(OnMegaphoneInput);
        // Stories-TTS-Start
        SubscribeLocalEvent<RMCMegaphoneUserComponent, EntitySpokeEvent>(OnEntitySpoke, after: new[] { typeof(TTSSystem) });
        SubscribeLocalEvent<RMCMegaphoneUserComponent, GetTTSPlaybackModifiersEvent>(OnGetTTSPlaybackModifiers);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        // Stories-TTS-End
    }

    private void OnMegaphoneInput(Entity<ActorComponent> ent, ref MegaphoneInputEvent ev)
    {
        if (_timing.ApplyingState)
            return;

        if (string.IsNullOrWhiteSpace(ev.Message))
            return;

        var user = GetEntity(ev.Actor);
        EnsureComp<RMCSpeechBubbleSpecificStyleComponent>(user);
        var userComp = EnsureComp<RMCMegaphoneUserComponent>(user);
        // Stories-TTS-Start
        userComp.VoiceRangeMultiplier = ev.VoiceRangeMultiplier;
        userComp.TTSVolumeMultiplier = ev.TTSVolumeMultiplier;
        userComp.TTSRangeMultiplier = ev.TTSRangeMultiplier;
        userComp.TTSReferenceDistance = ev.TTSReferenceDistance;
        userComp.TTSRolloffFactor = ev.TTSRolloffFactor;
        userComp.AudioEffects = ev.AudioEffects;
        Dirty(user, userComp);
        // Stories-TTS-End

        if (TryComp<SpeechComponent>(user, out var speech))
        {
            userComp.OriginalSpeechVerb = speech.SpeechVerb;
            userComp.OriginalSpeechSounds = speech.SpeechSounds;
            userComp.OriginalSuffixSpeechVerbs = speech.SuffixSpeechVerbs;

            speech.SpeechVerb = userComp.SpeechVerb;
            speech.SpeechSounds = userComp.MegaphoneSpeechSound;
            speech.SuffixSpeechVerbs = userComp.SuffixSpeechVerbs;
            Dirty(user, speech);

            // Send a message using the say command
            var session = ent.Comp.PlayerSession;
            _console.ExecuteCommand(session, $"say \"{CommandParsing.Escape(ev.Message)}\"");

            // Restore the original speech settings
            speech.SpeechVerb = userComp.OriginalSpeechVerb ?? "Default";
            speech.SpeechSounds = userComp.OriginalSpeechSounds;
            speech.SuffixSpeechVerbs = userComp.OriginalSuffixSpeechVerbs ?? new();
            Dirty(user, speech);
        }
    }

    private void OnEntitySpoke(Entity<RMCMegaphoneUserComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Channel != null)
            return;

        // Remove components after the message is sent
        RemComp<RMCMegaphoneUserComponent>(ent);
        RemComp<RMCSpeechBubbleSpecificStyleComponent>(ent);
    }

    // Stories-TTS-Start
    private void OnGetTTSPlaybackModifiers(Entity<RMCMegaphoneUserComponent> ent, ref GetTTSPlaybackModifiersEvent args)
    {
        args.AddVolumeMultiplier(ent.Comp.TTSVolumeMultiplier);
        args.AddRangeMultiplier(ent.Comp.TTSRangeMultiplier);
        args.SetReferenceDistance(ent.Comp.TTSReferenceDistance);
        args.SetRolloffFactor(ent.Comp.TTSRolloffFactor);
        args.AddAudioEffect(ent.Comp.AudioEffects);
    }
    // Stories-TTS-End

    // Stories-TTS-Start
    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (!TryComp<RMCMegaphoneUserComponent>(ev.Source, out var megaphoneUser))
            return;

        var megaphoneRange = ev.VoiceRange * megaphoneUser.VoiceRangeMultiplier;

        var sourceTransform = Transform(ev.Source);
        var sourcePos = _transform.GetWorldPosition(sourceTransform);
        var ghostHearing = GetEntityQuery<GhostHearingComponent>();
        var xforms = GetEntityQuery<TransformComponent>();

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            var transformEntity = xforms.GetComponent(playerEntity);

            if (transformEntity.MapID != sourceTransform.MapID)
                continue;

            var recipientPos = _transform.GetWorldPosition(transformEntity, xforms);
            var distance = (sourcePos - recipientPos).Length();
            var observer = ghostHearing.HasComponent(playerEntity);

            if (distance < megaphoneRange && distance >= ev.VoiceRange && !ev.Recipients.ContainsKey(player))
                ev.Recipients.TryAdd(player, new ICChatRecipientData(distance, observer));
        }
    }
    // Stories-TTS-End
}
