using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Stories.ARES.Interface;

public sealed partial class AresInterfaceSystem
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private void ConversationAction(Entity<AresTerminalComponent> terminal, Entity<AresInterfaceCoreComponent> core,
        AresAccess level, AresActionMessage message)
    {
        if (message.Action == AresAction.ReadConversation)
        {
            if (level >= AresAccess.HighCommand && core.Comp.Conversations.Any(c => c.Id == message.Record))
                terminal.Comp.ConversationId = message.Record;
            return;
        }
        if (terminal.Comp.Card is not { } card || terminal.Comp.Page != AresPage.Talk)
            return;
        var text = message.Text.Trim();
        if (message.Action == AresAction.SendMessage && (text.Length == 0 || text.Length > core.Comp.MaxTextLength ||
            core.Comp.NextMessages.TryGetValue(card, out var ready) && _timing.CurTime < ready))
            return;
        var conversation = core.Comp.Conversations.FirstOrDefault(c => !c.Archived && c.Card == card && c.User == terminal.Comp.User);
        if (message.Action == AresAction.SendMessage && conversation == null)
        {
            conversation = new AresConversation(core.Comp.NextConversationId++, card, terminal.Comp.User);
            AppendBounded(core.Comp.Conversations, conversation, core.Comp.MaxConversations);
            AppendBounded(conversation.Lines, new AresTalkLine(_ticker.RoundDuration(), "ARES",
                Loc.GetString("stories-ares-greeting", ("name", conversation.User))), core.Comp.MaxLines);
            terminal.Comp.ConversationId = conversation.Id;
        }
        if (conversation == null) return;
        if (message.Action == AresAction.ClearConversation)
        {
            conversation.Archived = true;
            ConversationsChanged();
            terminal.Comp.ConversationId = null;
            Audit(core, RealUser(terminal), Loc.GetString("stories-ares-conversation-archived", ("id", conversation.Id)));
            return;
        }
        if (message.Action != AresAction.SendMessage) return;
        core.Comp.NextMessages[card] = _timing.CurTime + core.Comp.MessageCooldown;
        AppendBounded(conversation.Lines, new AresTalkLine(_ticker.RoundDuration(), terminal.Comp.User, text), core.Comp.MaxLines);
        var announcement = Loc.GetString("stories-ares-admin-message", ("core", GetNetEntity(core).ToString()),
            ("id", conversation.Id), ("user", terminal.Comp.User), ("text", text));
        NotifyAdmins(core, announcement);
        ConversationsChanged();
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"ARES 1:1: {ToPrettyString(terminal.Comp.Operator):user} as {terminal.Comp.User} sent to conversation {conversation.Id}: {text}");
    }

    private void RefreshConversation(Entity<AresTerminalComponent> terminal, Entity<AresInterfaceCoreComponent> core, AresAccess access)
    {
        terminal.Comp.Conversation = [];
        AresConversation? conversation = null;
        if (terminal.Comp.Page == AresPage.Talk)
            conversation = core.Comp.Conversations.FirstOrDefault(c => !c.Archived && c.Card == terminal.Comp.Card && c.User == terminal.Comp.User);
        else if (terminal.Comp.Page is AresPage.Conversations or AresPage.DeletedConversations && access >= AresAccess.HighCommand)
            conversation = core.Comp.Conversations.FirstOrDefault(c => c.Id == terminal.Comp.ConversationId &&
                c.Archived == (terminal.Comp.Page == AresPage.DeletedConversations));
        terminal.Comp.ConversationId = conversation?.Id;
        if (conversation != null) terminal.Comp.Conversation = new(conversation.Lines);
    }

    private void NotifyAdmins(Entity<AresInterfaceCoreComponent> core, string text, bool sound = true)
    {
        foreach (var admin in _admins.ActiveAdmins)
        {
            if (_admins.HasAdminFlag(admin, AdminFlags.Fun)) _chat.SendAdminAnnouncementMessage(admin, text);
        }
        if (sound)
            _audio.PlayGlobal(core.Comp.NotificationSound,
                Filter.Empty().AddPlayers(_admins.ActiveAdmins.Where(a => _admins.HasAdminFlag(a, AdminFlags.Fun))), false);
    }

    public bool Reply(ICommonSession admin, EntityUid coreUid, int id, string text)
    {
        if (!_admins.HasAdminFlag(admin, AdminFlags.Fun) || !TryComp(coreUid, out AresInterfaceCoreComponent? component)) return false;
        Entity<AresInterfaceCoreComponent> core = (coreUid, component);
        var conversation = core.Comp.Conversations.FirstOrDefault(c => c.Id == id && !c.Archived);
        text = text.Trim();
        if (conversation == null || text.Length == 0 || text.Length > core.Comp.MaxTextLength) return false;
        AppendBounded(conversation.Lines, new AresTalkLine(_ticker.RoundDuration(), "ARES", text), core.Comp.MaxLines);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"ARES: admin {admin.Name} replied to conversation {id}: {text}");
        foreach (var terminal in core.Comp.Terminals)
        {
            if (TryComp(terminal, out AresTerminalComponent? comp) && comp.Card == conversation.Card && comp.Operator != null)
                _audio.PlayPvs(core.Comp.NotificationSound, terminal);
        }
        RefreshCore(core);
        ConversationsChanged();
        NotifyAdmins(core, Loc.GetString("stories-ares-admin-replied", ("admin", admin.Name), ("user", conversation.User), ("text", text)), sound: false);
        return true;
    }

}
