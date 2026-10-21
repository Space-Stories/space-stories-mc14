using Content.Server.Chat.Systems;
using Content.Shared._Stories.Synth;
using Content.Shared._Stories.Synth.VoiceSynthesizer;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Speech.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Synth.VoiceSynthesizer;

public sealed class STSynthVoiceSynthesizerSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<STSynthVoiceSynthesizerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<STSynthVoiceSynthesizerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<STSynthVoiceSynthesizerComponent, STSynthVoiceOpenEvent>(OnOpen);
        SubscribeLocalEvent<STSynthVoiceSynthesizerComponent, STSynthVoicePlayLineMsg>(OnPlayLine);
        SubscribeLocalEvent<STSynthVoiceSynthesizerComponent, STJobVariantGearAppliedEvent>(OnVariantGearApplied);
    }

    private void OnVariantGearApplied(Entity<STSynthVoiceSynthesizerComponent> ent, ref STJobVariantGearAppliedEvent args)
    {
        if (!ent.Comp.AlternateSoundVariants.Contains(args.Variant))
            return;

        ent.Comp.UseAlternateSound = true;
        Dirty(ent);

        if (ent.Comp.AlternateEmoteSounds != null && TryComp(ent, out VocalComponent? vocal))
        {
            vocal.EmoteSounds = ent.Comp.AlternateEmoteSounds;
            Dirty(ent.Owner, vocal);
        }
    }

    private void OnStartup(Entity<STSynthVoiceSynthesizerComponent> ent, ref ComponentStartup args)
    {
        _ui.SetUi(ent.Owner, STSynthVoiceUIKey.Key, new InterfaceData("STSynthVoiceSynthesizerBui"));
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<STSynthVoiceSynthesizerComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnOpen(Entity<STSynthVoiceSynthesizerComponent> ent, ref STSynthVoiceOpenEvent args)
    {
        var remaining = ent.Comp.NextLineTime - _timing.CurTime;
        var onCooldown = remaining > TimeSpan.Zero;
        _ui.OpenUi(ent.Owner, STSynthVoiceUIKey.Key, ent.Owner);
        _ui.SetUiState(ent.Owner, STSynthVoiceUIKey.Key,
            new STSynthVoiceBuiState(onCooldown, ent.Comp.UseAlternateSound, onCooldown ? remaining : default));
        args.Handled = true;
    }

    private void OnPlayLine(Entity<STSynthVoiceSynthesizerComponent> ent, ref STSynthVoicePlayLineMsg args)
    {
        if (_timing.CurTime < ent.Comp.NextLineTime)
            return;

        if (!_actionBlocker.CanSpeak(ent.Owner))
            return;

        if (!_prototype.TryIndex<EntityPrototype>(args.LineId, out var lineProto) ||
            !lineProto.TryGetComponent(out STSynthVoiceLineComponent? line, _compFactory))
        {
            return;
        }

        ent.Comp.NextLineTime = _timing.CurTime + ent.Comp.Cooldown;
        Dirty(ent);

        var sound = ent.Comp.UseAlternateSound ? line.AlternateSound ?? line.Sound : line.Sound;
        _audio.PlayPvs(sound, ent.Owner);

        var text = Loc.GetString(line.Text);
        _chat.TrySendInGameICMessage(ent.Owner, text, InGameICChatType.Speak, hideChat: false, hideLog: false, ignoreActionBlocker: true);

        _ui.SetUiState(ent.Owner, STSynthVoiceUIKey.Key,
            new STSynthVoiceBuiState(true, ent.Comp.UseAlternateSound, ent.Comp.Cooldown));
    }
}
