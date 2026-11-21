using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.TTS;

/// <summary>
/// Defines configuration for TTS text sanitization rules (e.g. abbreviations to phonetics, transliteration, stripping characters).
/// </summary>
[Prototype("ttsSanitizeConfig")]
public sealed partial class TTSSanitizeConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Phonetic alphabet mapping (e.g. A -> Эй)
    /// </summary>
    [DataField]
    public Dictionary<string, string> PhoneticAlphabet { get; private set; } = new();

    /// <summary>
    /// Transliteration mapping (e.g. a -> а)
    /// </summary>
    [DataField]
    public Dictionary<string, string> ReverseTranslit { get; private set; } = new();

    /// <summary>
    /// Letter combinations for approximate English pronunciation, longest match first.
    /// </summary>
    [DataField]
    public Dictionary<string, string> MultiLetterRules { get; private set; } = new();

    /// <summary>
    /// Fallback mapping for individual Latin letters.
    /// </summary>
    [DataField]
    public Dictionary<string, string> SingleLetterRules { get; private set; } = new();

    /// <summary>
    /// Regex for characters allowed in output. E.g. @"[^a-zA-Zа-яА-ЯёЁ0-9,\-+?!. ]"
    /// All characters matching this regex will be removed.
    /// </summary>
    [DataField]
    public string AllowedCharsRegex { get; private set; } = @"[^a-zA-Zа-яА-ЯёЁ0-9,\-+?!. ]";

    /// <summary>
    /// List of replacements to apply to text.
    /// </summary>
    [DataField]
    public List<TtsReplacementEntry> Replacements { get; private set; } = new();
}

[DataDefinition]
public sealed partial class TtsReplacementEntry
{
    [DataField]
    public string Pattern { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string ReplacedWith { get; private set; } = string.Empty;

    [DataField]
    public bool IsRegex { get; private set; }
}
