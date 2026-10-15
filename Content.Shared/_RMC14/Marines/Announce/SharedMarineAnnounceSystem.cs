using System.Text.RegularExpressions;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines.ControlComputer;
using Content.Shared._RMC14.Marines.GroundsideOperations;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Announce;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Survivor;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.Marines.Announce;

public abstract class SharedMarineAnnounceSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly ARESCoreSystem _core = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedMarineControlComputerSystem _marineControlComputer = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRankSystem _rankSystem = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedCMChatSystem _rmcChat = default!; // Stories-Chat

    public static readonly SoundSpecifier DefaultAnnouncementSound =
        new SoundPathSpecifier("/Audio/_RMC14/Announcements/Marine/notice2.ogg", AudioParams.Default.WithVolume(-2f));

    public static readonly SoundSpecifier DefaultSquadSound =
        new SoundPathSpecifier("/Audio/_RMC14/Effects/tech_notification.ogg", AudioParams.Default.WithVolume(-2f));
    public static readonly SoundSpecifier AresAnnouncementSound = new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg");

    public int CharacterLimit = 1000;

    private static readonly EntProtoId<ARESLogTypeComponent> LogCat = "ARESTabAnnouncementLogs";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarineCommunicationsComputerComponent, MarineCommunicationsAnnouncementDialogEvent>(OnMarineCommunicationsAnnouncementDialog);
        SubscribeLocalEvent<MarineCommunicationsComputerComponent, EchoSquadReasonEvent>(OnEchoSquadReason);
        SubscribeLocalEvent<MarineCommunicationsComputerComponent, EchoSquadConfirmEvent>(OnEchoSquadConfirm);

        Subs.BuiEvents<MarineCommunicationsComputerComponent>(MarineCommunicationsComputerUI.Key,
            subs =>
            {
                subs.Event<MarineCommunicationsComputerMsg>(OnMarineCommunicationsComputerMsg);
                subs.Event<MarineCommunicationsOpenAnnouncementMsg>(OnMarineCommunicationsOpenAnnouncementMsg);
                subs.Event<MarineCommunicationsOpenMapMsg>(OnMarineCommunicationsOpenMapMsg);
                subs.Event<MarineCommunicationsEchoSquadMsg>(OnMarineCommunicationsEchoMsg);
                subs.Event<MarineCommunicationsOverwatchMsg>(OnMarineCommunicationsOverwatchMsg);
                subs.Event<MarineControlComputerMedalMsg>(OnMarineCommunicationsMedalMsg);
            });
        Subs.BuiEvents<GroundsideOperationsConsoleComponent>(GroundsideOperationsConsoleUi.Key,
            subs =>
            {
                subs.Event<MarineCommunicationsComputerMsg>(OnGroundsideOperationsComputerMsg);
                subs.Event<MarineCommunicationsOpenAnnouncementMsg>(OnGroundsideOperationsOpenAnnouncementMsg);
                subs.Event<MarineCommunicationsOpenMapMsg>(OnGroundsideOperationsOpenMapMsg);
                subs.Event<MarineCommunicationsEchoSquadMsg>(OnGroundsideOperationsEchoMsg);
                subs.Event<MarineCommunicationsOverwatchMsg>(OnGroundsideOperationsOverwatchMsg);
                subs.Event<MarineControlComputerMedalMsg>(OnGroundsideOperationsMedalMsg);
            });

        Subs.CVar(_config, CCVars.ChatMaxMessageLength, limit => CharacterLimit = limit, true);
    }

    private void OnEchoSquadReason(Entity<MarineCommunicationsComputerComponent> ent, ref EchoSquadReasonEvent args)
    {
        if (!ent.Comp.CanCreateEcho)
            return;

        if (!TryGetEntity(args.User, out var user))
            return;

        var ev = new EchoSquadConfirmEvent(args.User, args.Message);
        _dialog.OpenConfirmation(
            ent,
            user.Value,
            Loc.GetString("rmc-echo-squad-confirm-title"),
            Loc.GetString("rmc-echo-squad-confirm-message", ("purpose", args.Message)),
            ev
        );
    }

    private void OnEchoSquadConfirm(Entity<MarineCommunicationsComputerComponent> ent, ref EchoSquadConfirmEvent args)
    {
        if (!ent.Comp.CanCreateEcho)
            return;

        if (!TryGetEntity(args.User, out var user))
            return;

        ent.Comp.CanCreateEcho = false;
        Dirty(ent);

        if (_squad.HasSquad(SquadSystem.EchoSquadId))
            return;

        _squad.TryEnsureSquad(SquadSystem.EchoSquadId, out _);
        _adminLog.Add(LogType.RMCSquadCreated, $"Echo squad was created by {ToPrettyString(user)} with reason {args.Message}");
    }

    private void OnMarineCommunicationsComputerMsg(Entity<MarineCommunicationsComputerComponent> ent, ref MarineCommunicationsComputerMsg args)
    {
        TryAnnounce(ent, args.Actor, args.Text, true);
    }

    private void OnMarineCommunicationsOpenAnnouncementMsg(
        Entity<MarineCommunicationsComputerComponent> ent,
        ref MarineCommunicationsOpenAnnouncementMsg args)
    {
        TryOpenAnnouncementDialog(ent, args.Actor);
    }

    private void OnGroundsideOperationsOpenAnnouncementMsg(
        Entity<GroundsideOperationsConsoleComponent> ent,
        ref MarineCommunicationsOpenAnnouncementMsg args)
    {
        if (!TryComp(ent, out MarineCommunicationsComputerComponent? communications))
            return;

        TryOpenAnnouncementDialog((ent.Owner, communications), args.Actor);
    }

    private void TryOpenAnnouncementDialog(Entity<MarineCommunicationsComputerComponent> ent, EntityUid actor)
    {
        if (!CanAnnounce(ent, actor))
            return;

        var ev = new MarineCommunicationsAnnouncementDialogEvent(GetNetEntity(actor));
        _dialog.OpenInput(
            ent.Owner,
            actor,
            Loc.GetString("rmc-announcement-shipside-header"),
            ev,
            true,
            CharacterLimit);
    }

    private void OnMarineCommunicationsAnnouncementDialog(
        Entity<MarineCommunicationsComputerComponent> ent,
        ref MarineCommunicationsAnnouncementDialogEvent args)
    {
        if (GetEntity(args.User) is not { Valid: true } user)
            return;

        TryAnnounce(ent, user, args.Message, false);
    }

    private void TryAnnounce(
        Entity<MarineCommunicationsComputerComponent> ent,
        EntityUid actor,
        string message,
        bool closeCommunicationsUi)
    {
        message = message.Trim();
        if (message.Length == 0 || !CanAnnounce(ent, actor))
            return;

        if (message.Length > CharacterLimit)
            message = message[..CharacterLimit].Trim();

        // Stories-Chat-Start
        message = Regex.Replace(message, @"\[/?.*?\]", "");
        message = _rmcChat.SanitizeMessageReplaceWords(actor, message);
        // Stories-Chat-End

        if (closeCommunicationsUi)
            _ui.CloseUi(ent.Owner, MarineCommunicationsComputerUI.Key);
        AnnounceSigned(actor, message, name: ent.Comp.AnnounceName,
            options: new SignedAnnouncementOptions { SendOverlay = ent.Comp.SendAnnouncementOverlay });

        ent.Comp.LastAnnouncement = _timing.CurTime;
        Dirty(ent);
    }

    private bool CanAnnounce(Entity<MarineCommunicationsComputerComponent> ent, EntityUid actor)
    {
        if (!_skills.HasSkill(actor, ent.Comp.AnnounceSkill, ent.Comp.AnnounceSkillLevel))
        {
            _popup.PopupClient(Loc.GetString("rmc-skills-no-training", ("target", ent)), actor, PopupType.MediumCaution);
            return false;
        }

        var time = _timing.CurTime;
        if (ent.Comp.LastAnnouncement is not { } last || time >= last + ent.Comp.Cooldown)
            return true;

        var seconds = Math.Max(1, (int) Math.Ceiling((last + ent.Comp.Cooldown - time).TotalSeconds));
        var cooldownMessage = Loc.GetString("rmc-announcement-cooldown", ("seconds", seconds));
        _popup.PopupClient(cooldownMessage, actor, PopupType.SmallCaution);
        return false;
    }

    private void OnMarineCommunicationsOpenMapMsg(Entity<MarineCommunicationsComputerComponent> ent, ref MarineCommunicationsOpenMapMsg args)
    {
        _ui.TryOpenUi(ent.Owner, TacticalMapComputerUi.Key, args.Actor);
    }

    private void OnGroundsideOperationsComputerMsg(
        Entity<GroundsideOperationsConsoleComponent> ent,
        ref MarineCommunicationsComputerMsg args)
    {
        if (TryComp(ent, out MarineCommunicationsComputerComponent? communications))
            OnMarineCommunicationsComputerMsg((ent.Owner, communications), ref args);
    }

    private void OnGroundsideOperationsOpenMapMsg(
        Entity<GroundsideOperationsConsoleComponent> ent,
        ref MarineCommunicationsOpenMapMsg args)
    {
        if (TryComp(ent, out MarineCommunicationsComputerComponent? communications))
            OnMarineCommunicationsOpenMapMsg((ent.Owner, communications), ref args);
    }

    private void OnMarineCommunicationsEchoMsg(Entity<MarineCommunicationsComputerComponent> ent, ref MarineCommunicationsEchoSquadMsg args)
    {
        if (!ent.Comp.CanCreateEcho)
            return;

        if (_squad.HasSquad(SquadSystem.EchoSquadId))
            return;

        var ev = new EchoSquadReasonEvent(GetNetEntity(args.Actor));
        _dialog.OpenInput(ent, args.Actor, Loc.GetString("rmc-goc-echo-purpose"), ev);
    }

    private void OnMarineCommunicationsOverwatchMsg(Entity<MarineCommunicationsComputerComponent> ent, ref MarineCommunicationsOverwatchMsg args)
    {
        if (!_skills.HasSkill(args.Actor, ent.Comp.OverwatchSkill, ent.Comp.OverwatchSkillLevel))
        {
            _popup.PopupClient(Loc.GetString("rmc-goc-overwatch-untrained"), args.Actor, PopupType.LargeCaution);
            return;
        }

        _ui.TryOpenUi(ent.Owner, OverwatchConsoleUI.Key, args.Actor);
    }

    private void OnGroundsideOperationsEchoMsg(
        Entity<GroundsideOperationsConsoleComponent> ent,
        ref MarineCommunicationsEchoSquadMsg args)
    {
        if (TryComp(ent, out MarineCommunicationsComputerComponent? communications))
            OnMarineCommunicationsEchoMsg((ent.Owner, communications), ref args);
    }

    private void OnGroundsideOperationsOverwatchMsg(
        Entity<GroundsideOperationsConsoleComponent> ent,
        ref MarineCommunicationsOverwatchMsg args)
    {
        if (TryComp(ent, out MarineCommunicationsComputerComponent? communications))
            OnMarineCommunicationsOverwatchMsg((ent.Owner, communications), ref args);
    }

    private void OnMarineCommunicationsMedalMsg(Entity<MarineCommunicationsComputerComponent> ent, ref MarineControlComputerMedalMsg args)
    {
        if (!ent.Comp.CanGiveMedals)
            return;

        _marineControlComputer.GiveMedal(ent.Owner, args.Actor);
    }

    // Stories-Chat-Start
    public Filter GetMarineFilter(Filter? filter = null, bool excludeSurvivors = true)
    {
        var recipients = filter == null
            ? Filter.Empty().AddWhereAttachedEntity(e =>
                HasComp<MarineComponent>(e) ||
                HasComp<GhostComponent>(e))
            : Filter.Empty().AddPlayers(filter.Recipients);

        if (excludeSurvivors)
            recipients.RemoveWhereAttachedEntity(HasComp<RMCSurvivorComponent>);

        // Non-rescued survivors must never receive marine announcements.
        recipients.RemoveWhereAttachedEntity(HasComp<IntelRescueSurvivorObjectiveComponent>);
        return recipients;
    }
    // Stories-Chat-End

    private void OnGroundsideOperationsMedalMsg(
        Entity<GroundsideOperationsConsoleComponent> ent,
        ref MarineControlComputerMedalMsg args)
    {
        if (TryComp(ent, out MarineCommunicationsComputerComponent? communications))
            OnMarineCommunicationsMedalMsg((ent.Owner, communications), ref args);
    }

    public virtual void AnnounceRadio(
        EntityUid sender,
        string message,
        ProtoId<RadioChannelPrototype> channel)
    {
    }

    public virtual void AnnounceARESStaging(
        EntityUid? source,
        string message,
        SoundSpecifier? sound = null,
        LocId? announcement = null)
    {
    }

    public void AnnounceARES(
        EntityUid? source,
        string message,
        SoundSpecifier? sound = null)
    {
        AnnounceARESStaging(source, message, sound, "rmc-announcement-ares-command");
    }

    public virtual void AnnounceSquad(
        string message,
        EntProtoId<SquadTeamComponent> squad,
        SoundSpecifier? sound = null)
    {
    }

    public virtual void AnnounceSquad(
        string message,
        EntityUid squad,
        SoundSpecifier? sound = null)
    {
    }

    public virtual void AnnounceSingle(
        string message,
        EntityUid receiver,
        SoundSpecifier? sound = null)
    {
    }

    public virtual void AnnounceOverwatchSquad(
        EntityUid sender,
        string message,
        EntityUid squad,
        SoundSpecifier? sound = null)
    {
    }

    public virtual void AnnounceAlertLevel(
        ProtoId<AnnouncementPresetPrototype> preset,
        string message,
        Filter? filter = null)
    {
    }

    /// <summary>
    ///     Dispatches already wrapped announcement to Marines.
    /// </summary>
    /// <param name="message">The content of the announcement.</param>
    /// <param name="sound">GlobalSound for announcement.</param>
    /// <param name="filter">Who should be able to see and hear the announcement.</param>
    // Stories-Chat-Start
    public virtual void AnnounceToMarines(
        string message,
        string wrappedMessage,
        SoundSpecifier? sound = null,
        Filter? filter = null,
        bool excludeSurvivors = true)
    {
    }

    public void AnnounceToMarines(
        string message,
        SoundSpecifier? sound = null,
        Filter? filter = null,
        bool excludeSurvivors = true)
    {
        AnnounceToMarines(message, message, sound, filter, excludeSurvivors);
    }
    // Stories-Chat-Start

    /// <summary>
    /// Dispatches an unsigned announcement to Marines.
    /// </summary>
    /// <param name="message">The content of the announcement.</param>
    /// <param name="author">The author of the message, UNMC High Command by default.</param>
    /// <param name="sound">GlobalSound for announcement.</param>
    public virtual void AnnounceHighCommand(
        string message,
        string? author = null,
        SoundSpecifier? sound = null,
        string? voiceId = null)
    {
    }

    /// <summary>
    /// Dispatches a signed announcement to Marines.
    /// </summary>
    /// <param name="sender">EntityUid of sender, for job and name params.</param>
    /// <param name="message">The content of the announcement.</param>
    /// <param name="author">The author of the message, Command by default.</param>
    /// <param name="name">The name to sign the message with, defaults to the name of <see cref="author"/>.</param>
    /// <param name="sound">GlobalSound for announcement.</param>
    /// <param name="filter">Who should be able to see and hear the announcement.</param>
    public virtual void AnnounceSigned(
        EntityUid sender,
        string message,
        string? author = null,
        string? name = null,
        SignedAnnouncementOptions? options = null)
    {
        if (_net.IsClient)
            return;

        options ??= new SignedAnnouncementOptions();
        author ??= Loc.GetString("rmc-announcement-author");
        name ??= _rankSystem.GetSpeakerFullRankName(sender) ?? Name(sender);
        var wrappedMessage = Loc.GetString("rmc-announcement-message-signed", ("author", author), ("message", message), ("name", name));

        DispatchSignedAnnouncement(sender, message, wrappedMessage, author, name, options);
        _adminLog.Add(LogType.RMCMarineAnnounce, $"{ToPrettyString(sender):source} marine announced message: {message}");

        if (_idCard.TryFindIdCard(sender, out var idCard) && TryComp(idCard, out ItemIFFComponent? idCardIFF))
            foreach (var faction in idCardIFF.Factions)
            {
                _core.CreateARESLog(faction, LogCat, $"{Name(sender)} sent an announcement: {message}");
            }
    }

    protected virtual void DispatchSignedAnnouncement(
        EntityUid sender,
        string message,
        string wrappedMessage,
        string author,
        string name,
        SignedAnnouncementOptions options)
    {
    }

    public string FormatHighCommand(string? author, string message)
    {
        author ??= Loc.GetString("rmc-announcement-author-highcommand");
        return Loc.GetString("rmc-announcement-message", ("author", author), ("message", message));
    }

    public string FormatARESStaging(LocId? author, string message)
    {
        author ??= "rmc-announcement-ares-message";
        return Loc.GetString(author, ("message", FormattedMessage.EscapeText(message)));
    }

    public string FormatARES(string message)
    {
        return FormatARESStaging("rmc-announcement-ares-command", message);
    }
}
