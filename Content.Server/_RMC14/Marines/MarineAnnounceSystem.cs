using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Rules.DistressSignal;
using Content.Server._Stories.TTS;
using Content.Server.Administration.Logs;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Announce;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.GroundsideOperations;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Marines;

public sealed class MarineAnnounceSystem : SharedMarineAnnounceSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogs = default!;
    [Dependency] private readonly AnnouncementRouterSystem _announcementRouter = default!;
    [Dependency] private readonly RMCAlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly ARESCoreSystem _core = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distressSignal = default!;
    [Dependency] private readonly SharedDropshipSystem _dropship = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    // Stories-TTS-Start
    [Dependency] private readonly TTSSystem _tts = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    // Stories-TTS-End

    private static readonly EntProtoId<ARESLogTypeComponent> LogCat = "ARESTabAnnouncementLogs";
    private static readonly ProtoId<AnnouncementPresetPrototype> PresetMarineOverwatch = "MarineOverwatch";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarineCommunicationsComputerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MarineCommunicationsComputerComponent, BoundUIOpenedEvent>(OnBUIOpened);

        SubscribeLocalEvent<RMCAlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<RMCPlanetComponent, RMCPlanetAddedEvent>(OnPlanetAdded);

        Subs.BuiEvents<MarineCommunicationsComputerComponent>(MarineCommunicationsComputerUI.Key,
            subs =>
            {
                subs.Event<MarineCommunicationsDesignatePrimaryLZMsg>(OnMarineCommunicationsDesignatePrimaryLZMsg);
            });
        Subs.BuiEvents<GroundsideOperationsConsoleComponent>(GroundsideOperationsConsoleUi.Key,
            subs =>
            {
                subs.Event<MarineCommunicationsDesignatePrimaryLZMsg>(OnGroundsideOperationsDesignatePrimaryLZMsg);
            });
    }

    private void OnMapInit(Entity<MarineCommunicationsComputerComponent> computer, ref MapInitEvent args)
    {
        UpdatePlanetMap(computer);
    }

    private void OnBUIOpened(Entity<MarineCommunicationsComputerComponent> computer, ref BoundUIOpenedEvent args)
    {
        UpdatePlanetMap(computer);
    }

    private void OnPlanetAdded(Entity<RMCPlanetComponent> ent, ref RMCPlanetAddedEvent args)
    {
        UpdateCommunicationsComputers();
    }

    private void OnAlertLevelChanged(ref RMCAlertLevelChangedEvent ev)
    {
        UpdateCommunicationsComputers();
    }

    private void UpdateCommunicationsComputers()
    {
        var computers = EntityQueryEnumerator<MarineCommunicationsComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
        {
            UpdatePlanetMap((uid, computer));
        }
    }

    private void OnMarineCommunicationsDesignatePrimaryLZMsg(
        Entity<MarineCommunicationsComputerComponent> computer,
        ref MarineCommunicationsDesignatePrimaryLZMsg args)
    {
        var user = args.Actor;
        if (!TryGetEntity(args.LZ, out var lz))
        {
            Log.Warning($"{ToPrettyString(user)} tried to designate invalid entity {args.LZ} as primary LZ!");
            return;
        }

        if (!_dropship.TryDesignatePrimaryLZ(user, lz.Value))
            return;

        _core.CreateARESLog(computer, LogCat, (string)$"{Name(args.Actor)} designated Primary LZ as: {Name(lz.Value)}");
    }

    private void OnGroundsideOperationsDesignatePrimaryLZMsg(
        Entity<GroundsideOperationsConsoleComponent> ent,
        ref MarineCommunicationsDesignatePrimaryLZMsg args)
    {
        if (TryComp(ent, out MarineCommunicationsComputerComponent? communications))
            OnMarineCommunicationsDesignatePrimaryLZMsg((ent.Owner, communications), ref args);
    }

    private void UpdatePlanetMap(Entity<MarineCommunicationsComputerComponent> computer)
    {
        computer.Comp.Planet = _distressSignal.SelectedPlanetMapName ?? string.Empty;
        computer.Comp.Operation = _distressSignal.OperationName ?? string.Empty;
        computer.Comp.LandingZones.Clear();

        foreach (var (id, metaData) in _dropship.GetPrimaryLZCandidates())
        {
            computer.Comp.LandingZones.Add(new LandingZone(GetNetEntity(id), metaData.EntityName));
        }

        computer.Comp.LandingZones.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        Dirty(computer);

        var distressBeaconEnabled = computer.Comp.CanTransmitDistress && _alertLevel.IsRedOrDeltaAlert();
        var state = new MarineCommunicationsComputerBuiState(
            computer.Comp.Planet, computer.Comp.Operation,
            new List<LandingZone>(computer.Comp.LandingZones), distressBeaconEnabled);
        _ui.SetUiState(computer.Owner, MarineCommunicationsComputerUI.Key, state);
    }

    public override void AnnounceToMarines(
        string message,
        string wrappedMessage,
        SoundSpecifier? sound = null,
        Filter? filter = null,
        bool excludeSurvivors = true)
    {
        filter = GetMarineFilter(filter, excludeSurvivors);

        var plainMessage = FormattedMessage.RemoveMarkupPermissive(message);

        _announcementRouter.Announce(new AnnouncementRequest
        {
            Message = message,
            Route = new AnnouncementRoute
            {
                Target = AnnouncementTarget.Marines,
                Channels = AnnouncementChannels.Chat | AnnouncementChannels.Sound,
            },
            Chat = new AnnouncementChatOptions
            {
                Message = plainMessage,
                WrappedMessage = wrappedMessage,
                Channel = ChatChannel.Radio,
            },
            Sound = CreateSoundOptions(sound ?? DefaultAnnouncementSound),
        }, filter);
    }

    // Stories-TTS-Start
    public override void AnnounceSigned(
        EntityUid sender,
        string message,
        string? author = null,
        string? name = null,
        SignedAnnouncementOptions? options = null)
    {
        options ??= new SignedAnnouncementOptions();
        base.AnnounceSigned(sender, message, author, name, options);

        var aresVoice = _configManager.GetCVar(SCCVars.TTSAresVoice);
        var authorVoice = _configManager.GetCVar(SCCVars.TTSCommandVoice);
        if (TryComp<TTSComponent>(sender, out var tts) && !string.IsNullOrEmpty(tts.VoicePrototypeId))
            authorVoice = tts.VoicePrototypeId;

        author ??= Loc.GetString("rmc-announcement-author");

        var headerMsg = Loc.GetString("tts-announce-header", ("author", author));
        var cleanHeader = FormattedMessage.RemoveMarkupPermissive(headerMsg).Trim();
        var wordCount = cleanHeader.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var delay = TimeSpan.FromSeconds(Math.Max(3.0, wordCount * 0.65));
        var recipientsFilter = GetMarineFilter(options.Filter, options.ExcludeSurvivors);

        if (!string.IsNullOrEmpty(aresVoice))
            _tts.PlayGlobalTTS(cleanHeader, aresVoice, recipientsFilter, TTSAudioEffect.Ares, isAnnounce: true);

        if (!string.IsNullOrEmpty(authorVoice))
        {
            Timer.Spawn(delay, () =>
            {
                _tts.PlayGlobalTTS(message, authorVoice, recipientsFilter, isAnnounce: true);
            });
        }
    }
    // Stories-TTS-End

    // Stories-TTS-Start
    public override void AnnounceHighCommand(
        string message,
        string? author = null,
        SoundSpecifier? sound = null,
        string? voiceId = null)
    {
        base.AnnounceHighCommand(message, author, sound, voiceId);

        var wrappedMessage = FormatHighCommand(author, message);
        AnnounceToMarines(message, wrappedMessage, sound);

        var aresVoice = _configManager.GetCVar(SCCVars.TTSAresVoice);
        var authorVoice = voiceId ?? _configManager.GetCVar(SCCVars.TTSHighCommandVoice);
        author ??= Loc.GetString("rmc-announcement-author-highcommand");

        var headerMsg = Loc.GetString("tts-announce-header", ("author", author));
        var cleanHeader = FormattedMessage.RemoveMarkupPermissive(headerMsg).Trim();
        var wordCount = cleanHeader.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var delay = TimeSpan.FromSeconds(Math.Max(3.0, wordCount * 0.65));
        var recipientsFilter = GetMarineFilter();

        if (!string.IsNullOrEmpty(aresVoice))
            _tts.PlayGlobalTTS(cleanHeader, aresVoice, recipientsFilter, TTSAudioEffect.Ares, isAnnounce: true);

        if (!string.IsNullOrEmpty(authorVoice))
        {
            Timer.Spawn(delay, () =>
            {
                _tts.PlayGlobalTTS(message, authorVoice, recipientsFilter, isAnnounce: true);
            });
        }
    }
    // Stories-TTS-End

    public override void AnnounceRadio(
        EntityUid sender,
        string message,
        ProtoId<RadioChannelPrototype> channel)
    {
        AnnounceRadio(sender, message, channel, true);
    }

    public void AnnounceRadio(
        EntityUid sender,
        string message,
        ProtoId<RadioChannelPrototype> channel,
        bool playTTS)
    {
        base.AnnounceRadio(sender, message, channel);

        _adminLogs.Add(LogType.RMCMarineAnnounce, $"{ToPrettyString(sender):source} marine announced radio message: {message}");
        _radio.SendRadioMessage(sender, message, channel, sender);
    }

    public override void AnnounceARESStaging(
        EntityUid? source,
        string message,
        SoundSpecifier? sound = null,
        LocId? announcement = null)
    {
        AnnounceARESStaging(source, message, sound, announcement, true);
    }

    public void AnnounceARESStaging(
        EntityUid? source,
        string message,
        SoundSpecifier? sound = null,
        LocId? announcement = null,
        bool playTTS = true)
    {
        base.AnnounceARESStaging(source, message, sound, announcement);

        var wrappedMessage = FormatARESStaging(announcement, message);

        AnnounceToMarines(message, wrappedMessage, sound);
        if (source != null)
            _adminLogs.Add(LogType.RMCMarineAnnounce, $"{ToPrettyString(source.Value):player} ARES announced message: {wrappedMessage}");

        var voice = _configManager.GetCVar(SCCVars.TTSAresVoice);

        var rawHeader = Loc.GetString(announcement ?? "rmc-announcement-ares-command", ("message", ""));
        var cleanHeader = FormattedMessage.RemoveMarkupPermissive(rawHeader).Trim();
        var ttsMessage = $"{cleanHeader} {message}";

        if (playTTS && !string.IsNullOrEmpty(voice))
            _tts.PlayGlobalTTS(ttsMessage, voice, GetMarineFilter(), TTSAudioEffect.Ares, isAnnounce: true);
    }

    public override void AnnounceSquad(string message, EntProtoId<SquadTeamComponent> squad, SoundSpecifier? sound = null)
    {
        base.AnnounceSquad(message, squad, sound);

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));
        AnnounceToFilter(message, filter, sound ?? DefaultSquadSound);
    }

    public override void AnnounceSquad(string message, EntityUid squad, SoundSpecifier? sound = null)
    {
        base.AnnounceSquad(message, squad, sound);

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));
        AnnounceToFilter(message, filter, sound ?? DefaultSquadSound);
    }

    public override void AnnounceSingle(string message, EntityUid receiver, SoundSpecifier? sound = null)
    {
        base.AnnounceSingle(message, receiver, sound);

        if (!TryComp(receiver, out ActorComponent? actor))
            return;

        var filter = Filter.Empty().AddPlayer(actor.PlayerSession);
        AnnounceToFilter(message, filter, sound);
    }

    protected override void DispatchSignedAnnouncement(
        EntityUid sender,
        string message,
        string wrappedMessage,
        string author,
        string name,
        SignedAnnouncementOptions options)
    {
        var dispatchFilter = GetMarineFilter(options.Filter, options.ExcludeSurvivors);

        var channels = AnnouncementChannels.Chat | AnnouncementChannels.Sound;
        if (options.SendOverlay)
            channels |= AnnouncementChannels.Overlay;
        _announcementRouter.Announce(new AnnouncementRequest
        {
            Message = message,
            Preset = AnnouncementRouterSystem.PresetMarineCommand,
            Route = new AnnouncementRoute
            {
                Target = AnnouncementTarget.Marines,
                Speaker = sender,
                Source = sender,
                Channels = channels,
            },
            Chat = new AnnouncementChatOptions
            {
                Message = FormattedMessage.RemoveMarkupPermissive(message),
                WrappedMessage = wrappedMessage,
                Channel = ChatChannel.Radio,
            },
            Sound = CreateSoundOptions(options.Sound),
        }, dispatchFilter);
    }

    public override void AnnounceOverwatchSquad(
        EntityUid sender,
        string message,
        EntityUid squad,
        SoundSpecifier? sound = null)
    {
        var color = Color.White;
        ProtoId<AnnouncementPresetPrototype> preset = PresetMarineOverwatch;

        if (TryComp(squad, out SquadTeamComponent? squadComp))
        {
            color = squadComp.AccessibleColor ?? squadComp.Color;
            preset = squadComp.OverwatchAnnouncementPreset;
        }

        var chatMessage = Loc.GetString(
            "rmc-overwatch-console-announce-message",
            ("color", color.ToHex()),
            ("operatorName", Name(sender)),
            ("message", message));

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));
        _announcementRouter.Announce(new AnnouncementRequest
        {
            Message = message,
            Preset = preset,
            Route = new AnnouncementRoute
            {
                Target = AnnouncementTarget.Marines,
                Speaker = sender,
                Source = sender,
                Channels = AnnouncementChannels.Chat | AnnouncementChannels.Overlay | AnnouncementChannels.Sound,
            },
            Chat = new AnnouncementChatOptions
            {
                Message = chatMessage,
                WrappedMessage = chatMessage,
                Channel = ChatChannel.Radio,
            },
            Sound = CreateSoundOptions(sound),
        }, filter);
    }

    public override void AnnounceAlertLevel(
        ProtoId<AnnouncementPresetPrototype> preset,
        string message,
        Filter? filter = null)
    {
        var request = new AnnouncementRequest
        {
            Message = message,
            Preset = preset,
            Route = new AnnouncementRoute
            {
                Target = AnnouncementTarget.Marines,
                Channels = AnnouncementChannels.Overlay,
            },
        };

        if (filter != null)
            _announcementRouter.Announce(request, filter);
        else
            _announcementRouter.Announce(request);
    }

    private void AnnounceToFilter(string message, Filter filter, SoundSpecifier? sound)
    {
        var plainMessage = FormattedMessage.RemoveMarkupPermissive(message);

        _announcementRouter.Announce(new AnnouncementRequest
        {
            Message = message,
            Route = new AnnouncementRoute
            {
                Target = AnnouncementTarget.Marines,
                Channels = AnnouncementChannels.Chat | AnnouncementChannels.Sound,
            },
            Chat = new AnnouncementChatOptions
            {
                Message = plainMessage,
                WrappedMessage = message,
                Channel = ChatChannel.Radio,
            },
            Sound = CreateSoundOptions(sound),
        }, filter);
    }

    private static AnnouncementSoundOptions? CreateSoundOptions(SoundSpecifier? sound)
    {
        if (sound == null)
            return null;

        return new AnnouncementSoundOptions
        {
            Sound = sound,
        };
    }
}
