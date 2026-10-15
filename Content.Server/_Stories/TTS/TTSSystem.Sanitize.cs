using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Chat.Systems;
using Content.Server._Stories.Chat;
using Content.Shared._Stories.TTS;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    private readonly Dictionary<string, string> _wordReplacement = new();
    private readonly List<(Regex Regex, string Replacement)> _regexReplacements = new();
    private readonly List<(string Pattern, string Replacement)> _multiLetterRules = new();
    private readonly Dictionary<char, string> _singleLetterRules = new();

    private TTSSanitizeConfigPrototype? _sanitizeConfig;

    private void InitializeSanitize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        BuildReplacements();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<TTSSanitizeConfigPrototype>())
            BuildReplacements();
    }

    private void BuildReplacements()
    {
        _wordReplacement.Clear();
        _regexReplacements.Clear();
        _multiLetterRules.Clear();
        _singleLetterRules.Clear();

        if (_prototypeManager.TryIndex("Default", out _sanitizeConfig))
        {
            foreach (var (pattern, replacement) in _sanitizeConfig.MultiLetterRules.OrderByDescending(rule => rule.Key.Length))
            {
                if (pattern.Length == 0)
                    continue;

                _multiLetterRules.Add((pattern, replacement));
            }

            foreach (var (letter, replacement) in _sanitizeConfig.SingleLetterRules)
            {
                if (letter.Length != 1)
                    continue;

                _singleLetterRules[char.ToLowerInvariant(letter[0])] = replacement;
            }

            Log.Info($"TTS Sanitize: successfully indexed Default config. Reading {_sanitizeConfig.Replacements.Count} replacements.");
            foreach (var replacement in _sanitizeConfig.Replacements)
            {
                if (replacement.IsRegex)
                {
                    var pattern = replacement.Pattern;
                    if (pattern.StartsWith(@"\b"))
                        pattern = @"(?<![a-zA-Zа-яА-ЯёЁ0-9_])" + pattern.Substring(2);
                    if (pattern.EndsWith(@"\b"))
                        pattern = pattern.Substring(0, pattern.Length - 2) + @"(?![a-zA-Zа-яА-ЯёЁ0-9_])";

                    _regexReplacements.Add((new Regex(pattern, RegexOptions.IgnoreCase), replacement.ReplacedWith));
                }
                else
                {
                    _wordReplacement[replacement.Pattern.ToLowerInvariant()] = replacement.ReplacedWith;
                }
            }
        }
        else
        {
            Log.Error("TTS Sanitize: failed to find 'Default' TTSSanitizeConfigPrototype!");
        }
    }

    private void OnTransformSpeech(ref TransformSpeechEvent args)
    {
        if (!_isEnabled)
            return;
    }

    private string Sanitize(string text)
    {
        text = text.Trim();

        var serverFilter = EntityManager.System<ServerChatFilterSystem>();
        text = serverFilter.CensorTTS(text);

        text = Regex.Replace(text, @"\[.*?\]", "");

        foreach (var (regex, replacement) in _regexReplacements)
        {
            text = regex.Replace(text, replacement);
        }

        text = Regex.Replace(text, @"\b[A-ZА-ЯЁ0-9]{1,6}\b", ReplaceAbbreviation);

        text = Regex.Replace(text, @"([^\p{P}])\s*\n", "$1. ");
        text = text.Replace("\n", " ");

        if (_sanitizeConfig != null && !string.IsNullOrEmpty(_sanitizeConfig.AllowedCharsRegex))
            text = Regex.Replace(text, _sanitizeConfig.AllowedCharsRegex, "");
        else
            text = Regex.Replace(text, @"[^a-zA-Zа-яА-ЯёЁ0-9,\-+?!. ]", "");
        text = Regex.Replace(text, @"(?<![a-zA-Zа-яёА-ЯЁ])[a-zA-Zа-яёА-ЯЁ]+?(?![a-zA-Zа-яёА-ЯЁ])", ReplaceMatchedWord, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?<=[0-9])(\.|,)(?=[0-9])", " целых ");
        text = Regex.Replace(text, @"\d+", ReplaceWord2Num);
        text = text.Trim();
        return text;
    }

    private string ReplaceAbbreviation(Match match)
    {
        var str = match.Value;

        var sb = new StringBuilder();
        var currentNum = "";

        foreach (var c in str)
        {
            if (char.IsDigit(c))
            {
                currentNum += c;
            }
            else
            {
                if (currentNum.Length > 0)
                {
                    sb.Append(NumberConverter.NumberToText(long.Parse(currentNum))).Append(" ");
                    currentNum = "";
                }
                if (_sanitizeConfig != null && _sanitizeConfig.PhoneticAlphabet.TryGetValue(char.ToUpperInvariant(c).ToString(), out var phon))
                    sb.Append(phon);
                else
                    sb.Append(c);
            }
        }
        if (currentNum.Length > 0)
            sb.Append(NumberConverter.NumberToText(long.Parse(currentNum))).Append(" ");

        return sb.ToString().TrimEnd();
    }

    private string ReplaceMatchedWord(Match word)
    {
        var value = word.Value;
        if (_wordReplacement.TryGetValue(value.ToLowerInvariant(), out var replace))
            return replace;

        if (_sanitizeConfig != null && IsLatinWord(value))
            return TtsLatinTransliterator.TransliterateWord(value, _multiLetterRules, _singleLetterRules, _sanitizeConfig.ReverseTranslit);

        return value;
    }

    private static bool IsLatinWord(string value)
    {
        foreach (var letter in value)
        {
            if (letter is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z'))
                return false;
        }

        return value.Length > 0;
    }

    private string ReplaceWord2Num(Match word)
    {
        if (!long.TryParse(word.Value, out var number))
            return word.Value;
        return NumberConverter.NumberToText(number);
    }


}

public static class TtsLatinTransliterator
{
    public static string TransliterateWord(
        string word,
        IReadOnlyList<(string Pattern, string Replacement)> multiLetterRules,
        IReadOnlyDictionary<char, string> singleLetterRules,
        IReadOnlyDictionary<string, string>? reverseTranslit = null)
    {
        if (string.IsNullOrWhiteSpace(word))
            return word;

        var normalized = word.Trim();
        if (reverseTranslit != null && reverseTranslit.TryGetValue(normalized.ToLowerInvariant(), out var configured))
            return configured;

        var builder = new StringBuilder(normalized.Length);
        for (var i = 0; i < normalized.Length;)
        {
            var matched = false;

            foreach (var (pattern, replacement) in multiLetterRules)
            {
                if (!normalized.AsSpan(i).StartsWith(pattern.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    continue;

                builder.Append(replacement);
                i += pattern.Length;
                matched = true;
                break;
            }

            if (matched)
                continue;

            var ch = normalized[i];
            builder.Append(singleLetterRules.TryGetValue(char.ToLowerInvariant(ch), out var mappedLetter)
                ? mappedLetter
                : ch);
            i++;
        }

        return PreserveInitialCase(normalized, builder.ToString());
    }

    private static string PreserveInitialCase(string source, string result)
    {
        if (result.Length == 0 || !char.IsUpper(source[0]))
            return result;

        return char.ToUpperInvariant(result[0]) + result.Substring(1);
    }
}

public static class NumberConverter
{
    private static readonly string[] Frac20Male =
    {
        "", "один", "два", "три", "четыре", "пять", "шесть",
        "семь", "восемь", "девять", "десять", "одиннадцать",
        "двенадцать", "тринадцать", "четырнадцать", "пятнадцать",
        "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"
    };

    private static readonly string[] Frac20Female =
    {
        "", "одна", "две", "три", "четыре", "пять", "шесть",
        "семь", "восемь", "девять", "десять", "одиннадцать",
        "двенадцать", "тринадцать", "четырнадцать", "пятнадцать",
        "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"
    };

    private static readonly string[] Hunds =
    {
        "", "сто", "двести", "триста", "четыреста",
        "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"
    };

    private static readonly string[] Tens =
    {
        "", "десять", "двадцать", "тридцать", "сорок", "пятьдесят",
        "шестьдесят", "семьдесят", "восемьдесят", "девяносто"
    };

    public static string NumberToText(long value, bool male = true)
    {
        if (value >= (long)Math.Pow(10, 15))
            return string.Empty;

        if (value == 0)
            return "ноль";

        var str = new StringBuilder();

        if (value < 0)
        {
            str.Append("минус");
            value = -value;
        }

        value = AppendPeriod(value, 1000000000000, str, "триллион", "триллиона", "триллионов", true);
        value = AppendPeriod(value, 1000000000, str, "миллиард", "миллиарда", "миллиардов", true);
        value = AppendPeriod(value, 1000000, str, "миллион", "миллиона", "миллионов", true);
        value = AppendPeriod(value, 1000, str, "тысяча", "тысячи", "тысяч", false);

        var hundreds = (int)(value / 100);
        if (hundreds != 0)
            AppendWithSpace(str, Hunds[hundreds]);

        var less100 = (int)(value % 100);
        var frac20 = male ? Frac20Male : Frac20Female;
        if (less100 < 20)
            AppendWithSpace(str, frac20[less100]);
        else
        {
            var tens = less100 / 10;
            AppendWithSpace(str, Tens[tens]);
            var less10 = less100 % 10;
            if (less10 != 0)
                str.Append(" " + frac20[less100 % 10]);
        }

        return str.ToString();
    }

    private static void AppendWithSpace(StringBuilder stringBuilder, string str)
    {
        if (stringBuilder.Length > 0)
            stringBuilder.Append(" ");
        stringBuilder.Append(str);
    }

    private static long AppendPeriod(
        long value,
        long power,
        StringBuilder str,
        string declension1,
        string declension2,
        string declension5,
        bool male)
    {
        var thousands = (int)(value / power);
        if (thousands > 0)
        {
            AppendWithSpace(str, NumberToText(thousands, male, declension1, declension2, declension5));
            return value % power;
        }
        return value;
    }

    private static string NumberToText(
        long value,
        bool male,
        string valueDeclensionFor1,
        string valueDeclensionFor2,
        string valueDeclensionFor5)
    {
        return
            NumberToText(value, male)
            + " "
            + GetDeclension((int)(value % 10), valueDeclensionFor1, valueDeclensionFor2, valueDeclensionFor5);
    }

    private static string GetDeclension(int val, string one, string two, string five)
    {
        var t = (val % 100 > 20) ? val % 10 : val % 20;

        return t switch
        {
            1 => one,
            2 or 3 or 4 => two,
            _ => five,
        };
    }
}
