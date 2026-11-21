using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Synth.VoiceSynthesizer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STSynthVoiceSynthesizerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "STActionWorkingJoeVoice";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool UseAlternateSound;

    [DataField]
    public HashSet<string> AlternateSoundVariants = new();

    [DataField]
    public ProtoId<EmoteSoundsPrototype>? AlternateEmoteSounds;

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan NextLineTime;
}

[Serializable, NetSerializable]
public enum STSynthVoiceUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class STSynthVoiceBuiState : BoundUserInterfaceState
{
    public readonly bool OnCooldown;
    public readonly bool UseAlternateSound;

    public readonly TimeSpan RemainingCooldown;

    public STSynthVoiceBuiState(bool onCooldown, bool useAlternateSound, TimeSpan remainingCooldown = default)
    {
        OnCooldown = onCooldown;
        UseAlternateSound = useAlternateSound;
        RemainingCooldown = remainingCooldown;
    }
}

[Serializable, NetSerializable]
public sealed class STSynthVoicePlayLineMsg : BoundUserInterfaceMessage
{
    public readonly string LineId;

    public STSynthVoicePlayLineMsg(string lineId)
    {
        LineId = lineId;
    }
}
