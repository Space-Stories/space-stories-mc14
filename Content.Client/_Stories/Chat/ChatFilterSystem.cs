using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._Stories.Chat;
using Content.Shared._Stories.SCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Chat;

public sealed class ChatFilterSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<string> _localBanwordsRaw = new();
    private readonly List<string> _serverBanwordsRaw = new();

    private readonly List<FilterRule> _localRules = new();
    private readonly List<FilterRule> _serverRules = new();

    private readonly Dictionary<string, string> _clientReplacements = new();
    private bool _streamerMode;

    private static readonly ResPath ConfigPath = new("/chat_filter_config.yml");
    private static readonly Regex TagRegex = new(@"\[[^\]]*\]", RegexOptions.Compiled);

    private sealed class FilterRule
    {
        public Regex Pattern = default!;
        public List<Regex> Exceptions = new();
    }

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(SCCVars.StreamerModeEnabled, v => _streamerMode = v, true);
        SubscribeNetworkEvent<SyncBanwordsEvent>(OnSyncBanwords);
        LoadConfig();
        RaiseNetworkEvent(new RequestBanwordsEvent());
    }

    private void OnSyncBanwords(SyncBanwordsEvent ev)
    {
        _serverBanwordsRaw.Clear();
        _serverRules.Clear();

        foreach (var word in ev.Banwords)
        {
            _serverBanwordsRaw.Add(word);
            var parts = word.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                var rule = new FilterRule { Pattern = BuildRegex(parts[0]) };
                for (int i = 1; i < parts.Length; i++)
                {
                    var exceptions = parts[i].Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var exc in exceptions)
                    {
                        rule.Exceptions.Add(BuildRegex(exc));
                    }
                }
                _serverRules.Add(rule);
            }
        }
    }

    public (string, string) GetCurrentConfigStrings()
    {
        var bWords = string.Join("\n", _localBanwordsRaw);
        var rWords = string.Join("\n", _clientReplacements.Select(kv => $"{kv.Key}={kv.Value}"));
        return (bWords, rWords);
    }

    public void SaveConfig(string bWordsRaw, string rWordsRaw)
    {
        _localBanwordsRaw.Clear();
        _localRules.Clear();
        _clientReplacements.Clear();

        var bWords = bWordsRaw.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var w in bWords)
        {
            var lower = w.ToLowerInvariant();
            _localBanwordsRaw.Add(lower);

            var parts = lower.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                var rule = new FilterRule { Pattern = BuildRegex(parts[0]) };
                for (int i = 1; i < parts.Length; i++)
                {
                    var exceptions = parts[i].Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var exc in exceptions)
                    {
                        rule.Exceptions.Add(BuildRegex(exc));
                    }
                }
                _localRules.Add(rule);
            }
        }

        var rLines = rWordsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in rLines)
        {
            var parts = line.Split('=');
            if (parts.Length == 2)
            {
                var from = parts[0].Trim().ToLowerInvariant();
                var to = parts[1].Trim();
                _clientReplacements[from] = to;
            }
        }

        var dir = ConfigPath.Directory;
        if (!_res.UserData.Exists(dir))
            _res.UserData.CreateDir(dir);

        using var writer = _res.UserData.OpenWriteText(ConfigPath);
        writer.WriteLine("banwords:");

        if (_localBanwordsRaw.Count == 0)
            writer.WriteLine("  []");
        else
        {
            foreach (var w in _localBanwordsRaw)
                writer.WriteLine($"  - \"{w.Replace("\"", "\\\"")}\"");
        }

        writer.WriteLine("replacements:");

        if (_clientReplacements.Count == 0)
            writer.WriteLine("  {}");
        else
        {
            foreach (var kv in _clientReplacements)
                writer.WriteLine($"  \"{kv.Key.Replace("\"", "\\\"")}\": \"{kv.Value.Replace("\"", "\\\"")}\"");
        }
    }

    private void LoadConfig()
    {
        _localBanwordsRaw.Clear();
        _localRules.Clear();
        _clientReplacements.Clear();

        if (!_res.UserData.Exists(ConfigPath))
        {
            SaveConfig(string.Empty, string.Empty);
            return;
        }

        try
        {
            using var reader = _res.UserData.OpenText(ConfigPath);
            var documents = DataNodeParser.ParseYamlStream(reader);
            var root = documents.FirstOrDefault()?.Root;

            if (root is MappingDataNode mapping)
            {
                foreach (var kvp in mapping)
                {
                    if (kvp.Key == "banwords" && kvp.Value is SequenceDataNode banSeq)
                    {
                        foreach (var node in banSeq.Sequence)
                        {
                            if (node is ValueDataNode valNode && !string.IsNullOrWhiteSpace(valNode.Value))
                            {
                                var lower = valNode.Value.ToLowerInvariant();
                                _localBanwordsRaw.Add(lower);

                                var parts = lower.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                if (parts.Length > 0)
                                {
                                    var rule = new FilterRule { Pattern = BuildRegex(parts[0]) };
                                    for (int i = 1; i < parts.Length; i++)
                                    {
                                        var exceptions = parts[i].Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                        foreach (var exc in exceptions)
                                        {
                                            rule.Exceptions.Add(BuildRegex(exc));
                                        }
                                    }
                                    _localRules.Add(rule);
                                }
                            }
                        }
                    }
                    else if (kvp.Key == "replacements" && kvp.Value is MappingDataNode repMap)
                    {
                        foreach (var rkvp in repMap)
                        {
                            if (rkvp.Value is ValueDataNode valNode
                                && !string.IsNullOrWhiteSpace(rkvp.Key) && !string.IsNullOrWhiteSpace(valNode.Value))
                            {
                                _clientReplacements[rkvp.Key.ToLowerInvariant()] = valNode.Value;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to load chat filter config: {e}");
        }
    }

    private Regex BuildRegex(string word)
    {
        var escaped = Regex.Escape(word);
        var pattern = escaped.Replace(@"\*", @"\w*");

        if (!word.StartsWith("*"))
            pattern = @"(?:^|\b)" + pattern;

        if (!word.EndsWith("*"))
            pattern = pattern + @"(?:\b|$)";

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private string GenerateCensor(int length)
    {
        var chars = "!#?@$%*&";
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[_random.Next(chars.Length)];
        return new string(result);
    }

    public string ApplyClientReplacements(string text)
    {
        foreach (var (from, to) in _clientReplacements)
        {
            var pattern = Regex.IsMatch(from, @"^\w+$") ? $@"\b{Regex.Escape(from)}\b" : Regex.Escape(from);
            text = Regex.Replace(text, pattern, to, RegexOptions.IgnoreCase);
        }
        return text;
    }

    public bool IsBanwordPresent(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var rule in _localRules.Concat(_serverRules))
        {
            var matches = rule.Pattern.Matches(text);
            foreach (Match match in matches)
            {
                bool isException = false;
                foreach (var exc in rule.Exceptions)
                {
                    var exMatches = exc.Matches(text);
                    foreach (Match exMatch in exMatches)
                    {
                        if (match.Index >= exMatch.Index && (match.Index + match.Length) <= (exMatch.Index + exMatch.Length))
                        {
                            isException = true;
                            break;
                        }
                    }
                    if (isException) break;
                }

                if (!isException) return true;
            }
        }
        return false;
    }

    public bool IsLocalBanwordPresent(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var rule in _localRules)
        {
            var matches = rule.Pattern.Matches(text);
            foreach (Match match in matches)
            {
                bool isException = false;
                foreach (var exc in rule.Exceptions)
                {
                    var exMatches = exc.Matches(text);
                    foreach (Match exMatch in exMatches)
                    {
                        if (match.Index >= exMatch.Index && (match.Index + match.Length) <= (exMatch.Index + exMatch.Length))
                        {
                            isException = true;
                            break;
                        }
                    }
                    if (isException) break;
                }

                if (!isException) return true;
            }
        }
        return false;
    }

    public string CensorMessage(string message, bool withTags = true)
    {
        if (!_streamerMode || string.IsNullOrWhiteSpace(message))
            return message;

        var tagMatches = withTags ? TagRegex.Matches(message) : null;

        foreach (var rule in _localRules.Concat(_serverRules))
        {
            var exceptionMatches = new List<Match>();
            foreach (var exc in rule.Exceptions)
            {
                foreach (Match m in exc.Matches(message))
                {
                    exceptionMatches.Add(m);
                }
            }

            message = rule.Pattern.Replace(message, match =>
            {
                if (tagMatches != null)
                {
                    foreach (Match tag in tagMatches)
                    {
                        if (match.Index < tag.Index + tag.Length && match.Index + match.Length > tag.Index)
                            return match.Value;
                    }
                }

                foreach (var exMatch in exceptionMatches)
                {
                    if (match.Index >= exMatch.Index && (match.Index + match.Length) <= (exMatch.Index + exMatch.Length))
                        return match.Value;
                }

                var censor = GenerateCensor(match.Length);
                return withTags ? $"[color=red]{censor}[/color]" : censor;
            });
        }

        return message;
    }
}
