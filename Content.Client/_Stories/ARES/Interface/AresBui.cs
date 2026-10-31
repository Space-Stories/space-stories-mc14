using System.Linq;
using Content.Client._RMC14.UserInterface.Crt;
using Content.Client._Stories.Hermes;
using Content.Shared._RMC14.UserInterface;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared._Stories.Hermes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Stories.ARES.Interface;

[UsedImplicitly]
public sealed class AresBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey), IRefreshableBui
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private AresWindow? _window;
    private HermesConfirmWindow? _confirmation;
    private AresPage _lastPage;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AresWindow>();
        _window.Login.OnPressed += _ => Send(AresAction.Login);
        _window.Logout.OnPressed += _ => Send(AresAction.Logout);
        _window.Logs.OnPressed += _ => Send(AresAction.OpenLogs);
        _window.TalkTab.OnPressed += _ => Select(AresPage.Talk);
        _window.SecurityTab.OnPressed += _ => Select(AresPage.CoreSecurity);
        _window.Send.OnPressed += _ =>
        {
            SendMessage(new AresActionMessage(AresAction.SendMessage, text: _window.Message.Text));
            _window.SetMessageText(string.Empty);
        };
        _window.Message.Input.OnTextChanged += _ => RefreshInput();
        _window.ClearConversation.OnPressed += _ => Confirm(new AresActionMessage(AresAction.ClearConversation),
            Loc.GetString("stories-ares-confirm-clear"));
        _window.CoreSecurity.Action += (action, text) => Confirm(new AresActionMessage(action.Action switch
        {
            HermesAction.Lockdown => AresAction.Lockdown,
            HermesAction.ReleaseGas => AresAction.ReleaseGas,
            _ => AresAction.ChangeIff,
        }, target: action.Target, iff: action.Iff), text);
        Refresh();
    }

    private void Send(AresAction action) => SendMessage(new AresActionMessage(action));

    private void Select(AresPage page) => SendMessage(new AresActionMessage(AresAction.SelectPage, page));

    public void Refresh()
    {
        if (_window == null) return;
        var hasData = EntMan.TryGetComponent(Owner, out AresTerminalComponent? data);
        var loggedIn = hasData && data!.Access != AresAccess.LoggedOut;
        _window.Login.Visible = !loggedIn;
        _window.Logout.Visible = _window.Program.Visible = loggedIn;
        _window.Logs.Visible = loggedIn;
        _window.LoginPanel.Visible = !loggedIn;
        _window.Identity.Text = loggedIn ? Loc.GetString("stories-ares-identity", ("user", data!.User),
            ("access", Loc.GetString($"stories-ares-access-{data.Access}"))) : Loc.GetString("stories-ares-title");
        _window.Feedback.Text = hasData && data!.Connected ? data.Feedback : Loc.GetString("stories-hermes-offline");
        if (!loggedIn) return;
        if (_lastPage != data!.Page)
        {
            _window.SetMessageText(string.Empty);
            _lastPage = data.Page;
        }
        _window.TalkTab.Selected = data.Page != AresPage.CoreSecurity;
        _window.SecurityTab.Selected = data.Page == AresPage.CoreSecurity;
        _window.SecurityTab.Visible = data.Pages.Any(page => page == AresPage.CoreSecurity);
        _window.CoreSecurity.Visible = data.Page == AresPage.CoreSecurity && data.Security != null;
        if (data.Page == AresPage.CoreSecurity && data.Security == null)
            _window.Feedback.Text = Loc.GetString("stories-hermes-offline");
        if (data.Page == AresPage.CoreSecurity && data.Security is { } security)
            _window.CoreSecurity.Refresh(security, _timing.CurTime, data.Access >= AresAccess.HighCommand);
        _window.Compose.Visible = data.Page == AresPage.Talk;
        _window.Entries.Visible = data.Page == AresPage.Talk;
        _window.ClearConversation.Disabled = data.ConversationId == null;
        _window.Entries.RemoveAllChildren();
        foreach (var line in data.Conversation)
        {
            var row = new HermesLogRow();
            row.Message.Text = $"{(int) line.Time.TotalHours:00}:{line.Time.Minutes:00}:{line.Time.Seconds:00} · {line.Author}\n{line.Text}";
            _window.Entries.AddChild(row);
        }
        _window.Empty.Visible = _window.Entries.ChildCount == 0 && data.Page == AresPage.Talk;
        RefreshInput();
    }

    private void RefreshInput()
    {
        if (_window == null) return;
        var max = EntMan.TryGetComponent(Owner, out AresTerminalComponent? data) ? data.MaxTextLength : 512;
        var length = _window.Message.Text.Trim().Length;
        _window.Counter.Text = Loc.GetString("stories-hermes-counter", ("length", length), ("max", max));
        _window.Counter.Tone = length > max ? RMCCrtTone.Danger : RMCCrtTone.Muted;
        _window.Send.Disabled = length == 0 || length > max;
    }

    private void Confirm(AresActionMessage message, string text)
    {
        _confirmation?.Close();
        _confirmation = new HermesConfirmWindow();
        var dialog = _confirmation;
        dialog.Message.Text = text;
        dialog.Accept.OnPressed += _ => { SendMessage(message); dialog.Close(); };
        dialog.Cancel.OnPressed += _ => dialog.Close();
        dialog.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        _confirmation?.Close();
        base.Dispose(disposing);
    }
}
