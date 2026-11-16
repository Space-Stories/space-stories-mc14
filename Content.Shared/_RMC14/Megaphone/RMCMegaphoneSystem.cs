using Content.Shared.Interaction.Events;
using Content.Shared._RMC14.Dialog;
using Content.Shared._Stories.TTS; // Stories-TTS
using Content.Shared.Examine;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Megaphone;

public sealed class RMCMegaphoneSystem : EntitySystem
{
    [Dependency] private readonly DialogSystem _dialog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCMegaphoneComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RMCMegaphoneComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUseInHand(Entity<RMCMegaphoneComponent> ent, ref UseInHandEvent args)
    {
        args.Handled = true;

        // Stories-TTS-Start
        var ev = new MegaphoneInputEvent(
            GetNetEntity(args.User),
            VoiceRangeMultiplier: ent.Comp.VoiceRangeMultiplier,
            TTSVolumeMultiplier: ent.Comp.TTSVolumeMultiplier,
            TTSRangeMultiplier: ent.Comp.TTSRangeMultiplier,
            TTSReferenceDistance: ent.Comp.TTSReferenceDistance,
            TTSRolloffFactor: ent.Comp.TTSRolloffFactor,
            AudioEffects: ent.Comp.AudioEffects);
        // Stories-TTS-End
        _dialog.OpenInput(args.User, Loc.GetString("rmc-megaphone-ui-text"), ev, largeInput: false, characterLimit: 150);
    }

    private void OnExamined(Entity<RMCMegaphoneComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("rmc-megaphone-examine"));
    }
}

[Serializable, NetSerializable]
// Stories-TTS-Start
public sealed record MegaphoneInputEvent(
    NetEntity Actor,
    string Message = "",
    float VoiceRangeMultiplier = 1.5f,
    float TTSVolumeMultiplier = 1.5f,
    float TTSRangeMultiplier = 1.5f,
    float TTSReferenceDistance = 4f,
    float TTSRolloffFactor = 0.25f,
    TTSAudioEffect AudioEffects = TTSAudioEffect.Megaphone) : DialogInputEvent(Message);
// Stories-TTS-End
