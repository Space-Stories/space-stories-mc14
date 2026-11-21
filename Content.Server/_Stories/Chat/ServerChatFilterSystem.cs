using System.IO;
using System.Text.RegularExpressions;
using Content.Shared._Stories.Chat;
using Content.Shared._Stories.SCCVars;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Chat;

public sealed class ServerChatFilterSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly List<string> _serverBanwordsRaw = new();
    private readonly List<FilterRule> _rules = new();
    private ISawmill _sawmill = default!;

    private sealed class FilterRule
    {
        public Regex Pattern = default!;
        public List<Regex> Exceptions = new();
    }

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("chat.filter");

        _cfg.OnValueChanged(SCCVars.BanwordsFile, OnFileChanged, true);

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<RequestBanwordsEvent>(OnRequestBanwords);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnRequestBanwords(RequestBanwordsEvent ev, EntitySessionEventArgs args)
    {
        if (_serverBanwordsRaw.Count > 0)
        {
            RaiseNetworkEvent(new SyncBanwordsEvent(_serverBanwordsRaw), args.SenderSession.Channel);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected || e.NewStatus == SessionStatus.InGame)
        {
            if (_serverBanwordsRaw.Count > 0)
            {
                RaiseNetworkEvent(new SyncBanwordsEvent(_serverBanwordsRaw), e.Session.Channel);
            }
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_serverBanwordsRaw.Count > 0)
        {
            RaiseNetworkEvent(new SyncBanwordsEvent(_serverBanwordsRaw), Filter.SinglePlayer(ev.Player));
        }
    }

    private void OnFileChanged(string filename)
    {
        _serverBanwordsRaw.Clear();
        _rules.Clear();

        if (string.IsNullOrWhiteSpace(filename))
            return;

        var path = new ResPath(filename).ToRootedPath();

        try
        {
            string content;
            var cleanFilename = filename.TrimStart('/');
            if (File.Exists(filename))
            {
                content = File.ReadAllText(filename);
            }
            else if (File.Exists(cleanFilename))
            {
                content = File.ReadAllText(cleanFilename);
            }
            else if (_res.UserData.Exists(path))
            {
                content = _res.UserData.ReadAllText(path);
            }
            else if (!path.ToString().Contains(":") && _res.ContentFileExists(path))
            {
                using var stream = _res.ContentFileRead(path);
                using var reader = new StreamReader(stream);
                content = reader.ReadToEnd();
            }
            else
            {
                _sawmill.Warning($"Banwords file not found at OS path, UserData, or Content: {path}");
                return;
            }

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var word = line.Trim();
                if (!string.IsNullOrWhiteSpace(word))
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
                        _rules.Add(rule);
                    }
                }
            }
            _sawmill.Info($"Loaded {_rules.Count} banword rules from {path}.");

            RaiseNetworkEvent(new SyncBanwordsEvent(_serverBanwordsRaw), Filter.Broadcast());
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load banwords from {path}: {e}");
        }
    }

    public string CensorTTS(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        foreach (var rule in _rules)
        {
            var exceptionMatches = new List<Match>();
            foreach (var exc in rule.Exceptions)
            {
                foreach (Match m in exc.Matches(text))
                {
                    exceptionMatches.Add(m);
                }
            }

            text = rule.Pattern.Replace(text, match =>
            {
                foreach (var exMatch in exceptionMatches)
                {
                    if (match.Index >= exMatch.Index && (match.Index + match.Length) <= (exMatch.Index + exMatch.Length))
                        return match.Value;
                }
                return "...";
            });
        }

        return text;
    }

    private Regex BuildRegex(string word)
    {
        var escaped = Regex.Escape(word);
        var pattern = escaped.Replace(@"\*", @"\w*");

        if (!word.StartsWith("*"))
            pattern = @"(?:^|\b)" + pattern;

        if (!word.EndsWith("*"))
            pattern = pattern + @"(?:\b|$)";

        pattern += @"(?![^\[]*\])";

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
