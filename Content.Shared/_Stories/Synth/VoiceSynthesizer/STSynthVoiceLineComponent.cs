using Robust.Shared.Audio;

namespace Content.Shared._Stories.Synth.VoiceSynthesizer;

[RegisterComponent]
public sealed partial class STSynthVoiceLineComponent : Component
{
    [DataField(required: true)]
    public LocId Category;

    [DataField(required: true)]
    public LocId Text;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public SoundSpecifier? AlternateSound;
}
