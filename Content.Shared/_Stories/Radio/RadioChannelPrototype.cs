// ReSharper disable CheckNamespace
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;

namespace Content.Shared.Radio;

public sealed partial class RadioChannelPrototype
{
    [DataField]
    public string? TtsCategory = "stories-tts-category-radio-general";

    [DataField]
    public bool ShowInTTSOptions = true;
}
