using System.Linq;
using Content.Client._RMC14.UserInterface.Crt;
using Content.Client._Stories.Hermes;
using Content.Client.Eui;
using Content.Shared._Stories.ARES.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.Log;

namespace Content.Client._Stories.ARES.Administration;

[UsedImplicitly]
public sealed partial class AresAdminEui : BaseEui
{
    private static readonly ISawmill Log = Logger.GetSawmill("ares-admin");
    private const int PageSize = 12;
    private const int PreviewLength = 96;
    private readonly Dictionary<(NetEntity Core, int Conversation), string> _drafts = [];
    private AresAdminWindow? _window;
    private HermesConfirmWindow? _confirmation;
    private AresAdminEuiState _state = new();
    private (NetEntity Core, int Conversation)? _selection;
    private Page _tab;
    private int _page;
    private bool _sending;
    private bool _followTranscriptEnd = true;

    private enum Page : byte { Conversations, Access }

    public override void Opened()
    {
        _window = new AresAdminWindow();
        _window.OnClose += OnWindowClosed;
        _window.Transcript.OnResized += OnTranscriptResized;
        _window.TranscriptScroll.OnScrolled += OnTranscriptScrolled;
        _window.Refresh.OnPressed += _ => SendMessage(new AresAdminRefreshMessage());
        _window.ConversationsTab.OnPressed += _ => Select(Page.Conversations);
        _window.AccessTab.OnPressed += _ => Select(Page.Access);
        _window.ShowArchived.OnToggled += _ => { _page = 0; Render(); };
        _window.ShowClosed.OnToggled += _ => { _page = 0; Render(); };
        _window.Reply.Input.OnTextChanged += _ =>
        {
            if (_selection is { } key)
                _drafts[key] = _window.Reply.Text;
            RefreshInput();
        };
        _window.Send.OnPressed += _ =>
        {
            if (_selection is not { } key || _window.Send.Disabled)
                return;
            _sending = true;
            SendMessage(new AresAdminReplyMessage(key.Core, key.Conversation, _window.Reply.Text));
            RefreshInput();
        };
        _window.Previous.OnPressed += _ => { _page = Math.Max(0, _page - 1); Render(); };
        _window.Next.OnPressed += _ => { _page++; Render(); };
        Render();
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AresAdminEuiState data)
            return;
        _state = data;
        foreach (var key in _drafts.Keys.ToArray())
        {
            if (!data.Conversations.Any(c => c.Core == key.Core && c.Id == key.Conversation))
                _drafts.Remove(key);
        }
        Render();
    }

    public override void HandleMessage(EuiMessageBase message)
    {
        if (message is AresAdminFocusMessage)
            _window?.MoveToFront();
        if (message is not AresAdminReplyResultMessage reply)
            return;
        _sending = false;
        var key = (reply.Core, reply.Conversation);
        if (reply.Success && _drafts.TryGetValue(key, out var draft) && draft == reply.Text)
        {
            _drafts.Remove(key);
            if (_selection == key && _window != null && _window.Reply.Text == reply.Text)
                _window.SetReplyText(string.Empty);
        }
        RefreshInput();
    }

    private void Select(Page page)
    {
        _tab = page;
        _page = 0;
        Render();
    }

    private void Render()
    {
        if (_window == null)
            return;
        try
        {
            _window.ConversationsTab.Selected = _tab == Page.Conversations;
            _window.AccessTab.Selected = _tab == Page.Access;
            _window.ConversationsPanel.Visible = _tab == Page.Conversations;
            _window.RequestsPanel.Visible = _tab != Page.Conversations;
            _window.Feedback.Text = _state.Feedback;
            _window.Conversations.RemoveAllChildren();
            _window.Requests.RemoveAllChildren();
            if (_tab == Page.Conversations)
                RenderConversations();
            else
                RenderAccessRequests();
            _window.Empty.Visible = _tab == Page.Conversations ? _window.Conversations.ChildCount == 0 : _window.Requests.ChildCount == 0;
            RefreshInput();
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to refresh ARES admin interface: {exception}");
        }
    }

    private IEnumerable<T> Paginate<T>(List<T> entries)
    {
        var pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        _window!.Previous.Disabled = _page == 0;
        _window.Next.Disabled = _page + 1 >= pages;
        _window.Page.Text = Loc.GetString("stories-hermes-page", ("page", _page + 1), ("pages", pages));
        return entries.Skip(_page * PageSize).Take(PageSize);
    }

    private void RenderConversations()
    {
        var window = _window!;
        var conversations = _state.Conversations.Where(c => window.ShowArchived.Pressed || !c.Archived)
            .OrderByDescending(c => c.LastActivity).ThenBy(c => c.Id).ToList();
        foreach (var conversation in Paginate(conversations))
        {
            var row = new AresAdminConversationRow();
            row.User.Text = $"#{conversation.Id} · {conversation.User}";
            row.Status.Text = Loc.GetString("stories-ares-admin-conversation-status", ("core", conversation.CoreName),
                ("status", Loc.GetString(conversation.Archived ? "stories-ares-admin-archived" : "stories-ares-admin-active")));
            var preview = conversation.Preview.Replace('\n', ' ');
            row.Preview.Text = preview.Length > PreviewLength ? preview[..PreviewLength] + "…" : preview;
            row.Read.Selected = _state.Selected is { } active && active.Core == conversation.Core && active.Id == conversation.Id;
            row.Read.OnPressed += _ => SendMessage(new AresAdminSelectMessage(conversation.Core, conversation.Id));
            window.Conversations.AddChild(row);
        }

        (NetEntity Core, int Conversation)? key = _state.Selected is { } selected ? (selected.Core, selected.Id) : null;
        if (_selection != key)
        {
            _followTranscriptEnd = true;
            _selection = key;
            window.SetReplyText(key is { } draftKey && _drafts.TryGetValue(draftKey, out var draft) ? draft : string.Empty);
        }
        window.SelectedUser.Text = _state.Selected is { } user ? Loc.GetString("stories-ares-admin-selected", ("user", user.User), ("id", user.Id))
            : Loc.GetString("stories-ares-admin-select-conversation");
        var scroll = window.TranscriptScroll.VScroll;
        window.Transcript.RemoveAllChildren();
        foreach (var line in _state.Transcript)
        {
            var row = new HermesLogRow();
            row.Message.Text = Loc.GetString("stories-hermes-log-entry", ("time", FormatTime(line.Time)),
                ("author", line.Author), ("message", line.Text));
            window.Transcript.AddChild(row);
        }
        window.TranscriptScroll.VScroll = scroll;
    }

    private void OnTranscriptResized()
    {
        if (_followTranscriptEnd && _window != null)
            _window.TranscriptScroll.VScroll = float.MaxValue;
    }

    private void OnTranscriptScrolled()
    {
        if (_window == null)
            return;
        var end = Math.Max(0, _window.Transcript.Size.Y - _window.TranscriptScroll.Size.Y);
        _followTranscriptEnd = _window.TranscriptScroll.VScroll >= end - 2;
    }

    private void RefreshInput()
    {
        if (_window == null)
            return;
        var length = _window.Reply.Text.Trim().Length;
        var max = _state.Selected?.MaxTextLength ?? 512;
        var writable = _state.Selected is { Archived: false };
        _window.Reply.Input.Editable = writable;
        _window.Send.Disabled = !writable || _sending || length == 0 || length > max;
        _window.Counter.Text = Loc.GetString("stories-hermes-counter", ("length", length), ("max", max));
        _window.Counter.Tone = length > max ? RMCCrtTone.Danger : RMCCrtTone.Muted;
    }

    private void Confirm(EuiMessageBase message, string text)
    {
        _confirmation?.Close();
        _confirmation = new HermesConfirmWindow();
        var dialog = _confirmation;
        dialog.Message.Text = text;
        dialog.Accept.OnPressed += _ => { SendMessage(message); dialog.Close(); };
        dialog.Cancel.OnPressed += _ => dialog.Close();
        dialog.OpenCentered();
    }

    private void OnWindowClosed() => SendMessage(new CloseEuiMessage());

    public override void Closed()
    {
        _confirmation?.Close();
        if (_window == null)
            return;
        _window.OnClose -= OnWindowClosed;
        _window.Transcript.OnResized -= OnTranscriptResized;
        _window.TranscriptScroll.OnScrolled -= OnTranscriptScrolled;
        _window.Close();
        _window.Dispose();
        _window = null;
    }

    private static string FormatTime(TimeSpan time) => $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}
