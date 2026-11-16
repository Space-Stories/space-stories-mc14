using System.Collections.Frozen;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Radio;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared._Stories.Hunter.Marking.Components; // Stories-Hunter

namespace Content.Shared.Chat;

public abstract class SharedChatSystem : EntitySystem
{
    public const char RadioCommonPrefix = ';';
    public const char RadioChannelPrefix = ':';
    public const char RadioChannelAltPrefix = '.';
    public const char LocalPrefix = '>';
    public const char ConsolePrefix = '/';
    public const char DeadPrefix = '\\';
    public const char LOOCPrefix = '(';
    public const char OOCPrefix = '[';
    public const char EmotesPrefix = '@';
    public const char EmotesAltPrefix = '*';
    public const char AdminPrefix = ']';
    public const char WhisperPrefix = ',';
    public const char MentorPrefix = '}';
    public const string DefaultChannelKey = "у"; // Stories-Hunter

    public static readonly ProtoId<RadioChannelPrototype> CommonChannel = "MarineCommon";
    public static readonly ProtoId<RadioChannelPrototype> HivemindChannel = "Hivemind";
    public static readonly ProtoId<RadioChannelPrototype> HunterChannel = "STHunter"; // Stories-Hunter

    public static readonly string DefaultChannelPrefix = $"{RadioChannelPrefix}{DefaultChannelKey}";
    public static readonly ProtoId<SpeechVerbPrototype> DefaultSpeechVerb = "Default";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly XenoEvolutionSystem _xenoEvolution = default!;

    // RMC14
    public FrozenDictionary<string, RadioChannelPrototype> _channelLookup = default!;
    public FrozenSet<char> _validPrefixes = default!;
    // RMC14
    /// <summary>
    /// Cache of the keycodes for faster lookup.
    /// </summary>
    public FrozenDictionary<string, RadioChannelPrototype> _keyCodes = default!; // Stories-Hunter

    public static readonly string[] DefaultChannelKeys = new[] { "у", "h" };

    public override void Initialize()
    {
        base.Initialize();
        DebugTools.Assert(_prototypeManager.HasIndex(CommonChannel));
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);
        CacheRadios();
    }

    protected virtual void OnPrototypeReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<RadioChannelPrototype>())
            CacheRadios();
    }

    private void CacheRadios()
    {
        // RMC14
        var channelDict = new Dictionary<string, RadioChannelPrototype>();
        var prefixSet = new HashSet<char>();
        var keyCodesDict = new Dictionary<string, RadioChannelPrototype>();

        foreach (var radioChannel in _prototypeManager.EnumeratePrototypes<RadioChannelPrototype>())
        {
            // RMC14 - The intercom channel is a chat routing sentinel, not a selectable radio channel.
            if (radioChannel.ID == RMCIntercomConstants.Channel)
                continue;
            // RMC14

            var keyCode = radioChannel.KeyCode.ToLowerInvariant();

            channelDict[$"{radioChannel.RadioPrefix}{keyCode}"] = radioChannel;
            prefixSet.Add(radioChannel.RadioPrefix);

            if (radioChannel.RadioPrefix == RadioChannelPrefix)
            {
                channelDict[$"{RadioChannelAltPrefix}{keyCode}"] = radioChannel;
                prefixSet.Add(RadioChannelAltPrefix);
            }

            if (!string.IsNullOrEmpty(keyCode))
            {
                keyCodesDict[keyCode] = radioChannel;
            }
        }

        _channelLookup = channelDict.ToFrozenDictionary();
        _validPrefixes = prefixSet.ToFrozenSet();
        _keyCodes = keyCodesDict.ToFrozenDictionary(); // Stories-Hunter: alias for compatibility
        // RMC14
    }

    /// <summary>
    ///     Attempts to find an applicable <see cref="SpeechVerbPrototype"/> for a speaking entity's message.
    ///     If one is not found, returns <see cref="DefaultSpeechVerb"/>.
    /// </summary>
    public SpeechVerbPrototype GetSpeechVerb(EntityUid source, string message, SpeechComponent? speech = null)
    {
        if (!Resolve(source, ref speech, false))
            return _prototypeManager.Index(DefaultSpeechVerb);

        // check for a suffix-applicable speech verb
        SpeechVerbPrototype? current = null;
        foreach (var (str, id) in speech.SuffixSpeechVerbs)
        {
            var proto = _prototypeManager.Index(id);
            if (message.EndsWith(Loc.GetString(str)) && proto.Priority >= (current?.Priority ?? 0))
            {
                current = proto;
            }
        }

        // if no applicable suffix verb return the normal one used by the entity
        return current ?? _prototypeManager.Index(speech.SpeechVerb);
    }

    /// <summary>
    /// Splits the input message into a radio prefix part and the rest to preserve it during sanitization.
    /// </summary>
    /// <remarks>
    /// This is primarily for the chat emote sanitizer, which can match against ":b" as an emote, which is a valid radio keycode.
    /// </remarks>
    public void GetRadioKeycodePrefix(EntityUid source,
        string input,
        out string output,
        out string prefix)
    {
        prefix = string.Empty;
        output = input;

        // If the string is less than 2, then it's probably supposed to be an emote.
        // No one is sending empty radio messages!
        if (input.Length <= 1) // Stories-Hunter
            return;

        // RMC14
        if (RMCIntercomConstants.HasPrefix(input))
        {
            prefix = input[..2];
            output = input[2..];
            return;
        }

        if (!_validPrefixes.Contains(input[0]))
            return;

        // Stories-Hunter-Start
        var messageWithoutPrefix = input[1..];
        string? foundKey = null;

        var allKeys = _keyCodes.Keys.Concat(DefaultChannelKeys).OrderByDescending(k => k.Length);
        foreach (var key in allKeys)
        {
            if (messageWithoutPrefix.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                foundKey = key;
                break;
            }
        }

        if (foundKey == null)
            return;
        // RMC14

        if (input.Length <= 1 + foundKey.Length)
            return;

        var prefixLength = 1 + foundKey.Length;
        prefix = input[..prefixLength];
        output = input[prefixLength..];
        // Stories-Hunter-End
    }

    /// <summary>
    ///     Attempts to resolve radio prefixes in chat messages (e.g., remove a leading ":e" and resolve the requested
    ///     channel. Returns true if a radio message was attempted, even if the channel is invalid.
    /// </summary>
    /// <param name="source">Source of the message</param>
    /// <param name="input">The message to be modified</param>
    /// <param name="output">The modified message</param>
    /// <param name="channel">The channel that was requested, if any</param>
    /// <param name="quiet">Whether or not to generate an informative pop-up message.</param>
    /// <returns></returns>
    public bool TryProccessRadioMessage(
        EntityUid source,
        string input,
        out string output,
        out RadioChannelPrototype? channel,
        bool quiet = false)
    {
        output = input.Trim();
        channel = null;

        var message = input.Trim();
        if (message.Length == 0)
            return false;

        var firstChar = message[0];

        if (firstChar == RadioCommonPrefix)
        {
            output = SanitizeMessageCapital(message[1..].TrimStart());

            // Stories-Hunter-Start
            if (HasComp<HunterComponent>(source))
            {
                channel = _prototypeManager.Index<RadioChannelPrototype>(HunterChannel);
            }
            else if (HasComp<XenoComponent>(source))
            {
                channel = _prototypeManager.Index<RadioChannelPrototype>(HivemindChannel);
            }
            else
            {
                channel = _prototypeManager.Index<RadioChannelPrototype>(CommonChannel);
            }
            // Stories-Hunter-End

            // RMC14
            if (channel?.ID == HivemindChannel.Id &&
                !_xenoEvolution.HasLiving<XenoEvolutionGranterComponent>(1))
            {
                if (!quiet)
                    _popup.PopupEntity(Loc.GetString("rmc-no-queen-hivemind-chat"), source, source, PopupType.LargeCaution);

                return false;
            }
            // RMC14

            return true;
        }

        // RMC14
        if (RMCIntercomConstants.HasPrefix(input))
        {
            output = SanitizeMessageCapital(input[2..].TrimStart());
            channel = _prototypeManager.Index<RadioChannelPrototype>(RMCIntercomConstants.Channel);
            return true;
        }

        if (!_validPrefixes.Contains(input[0]))
            return false;
        // RMC14

        // Stories-Hunter-Start
        var messageWithoutPrefix = message[1..];
        string? channelKey = null;

        var allKeys = _keyCodes.Keys.Concat(DefaultChannelKeys).OrderByDescending(k => k.Length);
        foreach (var key in allKeys)
        {
            if (messageWithoutPrefix.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                channelKey = key;
                break;
            }
        }

        if (channelKey == null)
        // Stories-Hunter-End
        {
            output = SanitizeMessageCapital(message[1..].TrimStart());
            if (HasComp<XenoComponent>(source))
                return false;

            if (!quiet)
                _popup.PopupEntity(Loc.GetString("chat-manager-no-radio-key"), source, source);
            return true;
        }

        output = SanitizeMessageCapital(message[(1 + channelKey.Length)..].TrimStart()); // Stories-Hunter

        var isDefaultChannel = DefaultChannelKeys.Contains(channelKey); // Stories-Hunter
        var lookupKey = $"{firstChar}{channelKey.ToLowerInvariant()}";
        var foundChannel = _channelLookup.TryGetValue(lookupKey, out channel);

        if (isDefaultChannel)
        {
            var ev = new GetDefaultRadioChannelEvent();
            RaiseLocalEvent(source, ev);

            if (ev.Channel != null)
                _prototypeManager.TryIndex(ev.Channel, out channel);

            if (ev.Channel == HivemindChannel.Id &&
                !_xenoEvolution.HasLiving<XenoEvolutionGranterComponent>(1))
            {
                if (!quiet)
                    _popup.PopupEntity(Loc.GetString("rmc-no-queen-hivemind-chat"), source, source, PopupType.LargeCaution);
                return false;
            }
        }
        else if (!foundChannel && !quiet)
        {
            var msg = Loc.GetString("chat-manager-no-such-channel", ("key", channelKey));
            _popup.PopupEntity(msg, source, source);
        }

        // RMC14
        if (channel?.ID == HivemindChannel.Id &&
            !_xenoEvolution.HasLiving<XenoEvolutionGranterComponent>(1))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("rmc-no-queen-hivemind-chat"), source, source, PopupType.LargeCaution);

            return false;
        }
        // RMC14

        var prefixEv = new ChatGetPrefixEvent(channel);
        RaiseLocalEvent(source, ref prefixEv);
        channel = prefixEv.Channel;

        if (HasComp<XenoComponent>(source) && channel == null)
            return false;
        // RMC14

        return true;
    }

    public string SanitizeMessageCapital(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;
        // Capitalize first letter
        message = OopsConcat(char.ToUpper(message[0]).ToString(), message.Remove(0, 1));
        return message;
    }

    private static string OopsConcat(string a, string b)
    {
        // This exists to prevent Roslyn being clever and compiling something that fails sandbox checks.
        return a + b;
    }

    public string SanitizeMessageCapitalizeTheWordI(string message, string theWordI = "i")
    {
        if (string.IsNullOrEmpty(message))
            return message;

        for
        (
            var index = message.IndexOf(theWordI);
            index != -1;
            index = message.IndexOf(theWordI, index + 1)
        )
        {
            // Stops the code If It's tryIng to capItalIze the letter I In the mIddle of words
            // Repeating the code twice is the simplest option
            if (index + 1 < message.Length && char.IsLetter(message[index + 1]))
                continue;
            if (index - 1 >= 0 && char.IsLetter(message[index - 1]))
                continue;

            var beforeTarget = message.Substring(0, index);
            var target = message.Substring(index, theWordI.Length);
            var afterTarget = message.Substring(index + theWordI.Length);

            message = beforeTarget + target.ToUpper() + afterTarget;
        }

        return message;
    }

    public static string SanitizeAnnouncement(string message, int maxLength = 0, int maxNewlines = 2)
    {
        var trimmed = message.Trim();
        if (maxLength > 0 && trimmed.Length > maxLength)
        {
            trimmed = $"{message[..maxLength]}...";
        }

        // No more than max newlines, other replaced to spaces
        if (maxNewlines > 0)
        {
            var chars = trimmed.ToCharArray();
            var newlines = 0;
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] != '\n')
                    continue;

                if (newlines >= maxNewlines)
                    chars[i] = ' ';

                newlines++;
            }

            return new string(chars);
        }

        return trimmed;
    }

    public static string InjectTagInsideTag(ChatMessage message, string outerTag, string innerTag, string? tagParameter)
    {
        var rawmsg = message.WrappedMessage;
        var tagStart = rawmsg.IndexOf($"[{outerTag}]");
        var tagEnd = rawmsg.IndexOf($"[/{outerTag}]");
        if (tagStart < 0 || tagEnd < 0) //If the outer tag is not found, the injection is not performed
            return rawmsg;
        tagStart += outerTag.Length + 2;

        string innerTagProcessed = tagParameter != null ? $"[{innerTag}={tagParameter}]" : $"[{innerTag}]";

        rawmsg = rawmsg.Insert(tagEnd, $"[/{innerTag}]");
        rawmsg = rawmsg.Insert(tagStart, innerTagProcessed);

        return rawmsg;
    }

    /// <summary>
    /// Injects a tag around all found instances of a specific string in a ChatMessage.
    /// Excludes strings inside other tags and brackets.
    /// </summary>
    public static string InjectTagAroundString(ChatMessage message, string targetString, string tag, string? tagParameter)
    {
        var rawmsg = message.WrappedMessage;
        rawmsg = Regex.Replace(rawmsg, "(?i)(" + targetString + ")(?-i)(?![^[]*])", $"[{tag}={tagParameter}]$1[/{tag}]");
        return rawmsg;
    }

    public static string GetStringInsideTag(ChatMessage message, string tag)
    {
        var rawmsg = message.WrappedMessage;
        var tagStart = rawmsg.IndexOf($"[{tag}]");
        var tagEnd = rawmsg.IndexOf($"[/{tag}]");
        if (tagStart < 0 || tagEnd < 0)
            return "";
        tagStart += tag.Length + 2;
        return rawmsg.Substring(tagStart, tagEnd - tagStart);
    }
}
