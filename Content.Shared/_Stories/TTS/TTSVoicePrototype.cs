using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.TTS;

/// <summary>
/// Prototype represent available TTS voices
/// </summary>
[Prototype("ttsVoice")]
// ReSharper disable once InconsistentNaming
public sealed class TTSVoicePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// List of categories forbidden to use this voice (e.g., "Human", "Hunter", "Xeno").
    /// If null or empty, available to everyone.
    /// </summary>
    [DataField]
    public HashSet<string>? Blacklist { get; private set; }

    [DataField]
    public string Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public Sex Sex { get; private set; }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(required: true)]
    public string Speaker { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the species is available "at round start" (In the character editor)
    /// </summary>
    [DataField]
    public bool RoundStart { get; private set; } = true;

    [DataField]
    public bool SponsorOnly { get; private set; }

    [DataField]
    public string Category { get; private set; } = "Uncategorized";
}
