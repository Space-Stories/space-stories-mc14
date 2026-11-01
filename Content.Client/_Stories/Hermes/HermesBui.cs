using System.Linq;
using Content.Client._RMC14.UserInterface.Crt;
using Content.Shared._RMC14.UserInterface;
using Content.Shared._Stories.Hermes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Stories.Hermes;

[UsedImplicitly]
public sealed class HermesBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey), IRefreshableBui
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int PageSize = 12;
    private HermesWindow? _window;
    private HermesConfirmWindow? _confirmation;
    private HermesAccess _access;
    private NetEntity? _card;
    private string _name = string.Empty;
    private bool _sound = true;
    private bool _connected = true;
    private Page _tab;
    private int _page;

    private enum Page : byte { Maintenance, Access, Logs, Logins, Security }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<HermesWindow>();
        _window.Login.OnPressed += _ => Send(HermesAction.Login);
        _window.Logout.OnPressed += _ => Send(HermesAction.Logout);
        _window.Sound.OnPressed += _ => Send(HermesAction.ToggleSound);
        _window.MaintenanceTab.OnPressed += _ => Select(Page.Maintenance);
        _window.AccessTab.OnPressed += _ => Select(Page.Access);
        _window.LogsTab.OnPressed += _ => Select(Page.Logs);
        _window.LoginsTab.OnPressed += _ => Select(Page.Logins);
        _window.SecurityTab.OnPressed += _ => Select(Page.Security);
        _window.AresLogs.OnPressed += _ => Send(HermesAction.OpenAresLogs);
        foreach (var category in Enum.GetValues<HermesCategory>())
            _window.Category.AddItem(Loc.GetString($"stories-hermes-category-{category}"), (int) category);
        _window.Category.SelectId((int) HermesCategory.BrokenLight);
        _window.Category.OnItemSelected += id => _window.Category.SelectId(id);
        _window.Details.Input.OnTextChanged += _ => RefreshSubmission();
        _window.ShowClosed.OnToggled += _ => { _page = 0; Refresh(); };
        _window.Submit.OnPressed += _ => Submit();
        _window.SecurityPanel.Action += Confirm;
        _window.PreviousPage.OnPressed += _ => { _page = Math.Max(0, _page - 1); Refresh(); };
        _window.NextPage.OnPressed += _ => { _page++; Refresh(); };
        Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        _confirmation?.Close();
        base.Dispose(disposing);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not HermesSessionMessage session)
            return;
        _access = session.Access;
        _card = session.Card;
        _name = session.Name;
        _sound = session.Sound;
        _connected = session.Connected;
        Refresh();
    }

    private void Send(HermesAction action) => SendMessage(new HermesActionMessage(action));

    private void Select(Page page)
    {
        _tab = page;
        _page = 0;
        Refresh();
    }

    public void Refresh()
    {
        if (_window == null)
            return;
        var loggedIn = _access != HermesAccess.LoggedOut;
        _window.Login.Visible = !loggedIn;
        _window.Logout.Visible = loggedIn;
        _window.Sound.Visible = loggedIn;
        _window.LoginPanel.Visible = !loggedIn;
        _window.Program.Visible = loggedIn;
        _window.Sound.Text = Loc.GetString(_sound ? "stories-hermes-sound-on" : "stories-hermes-sound-off");
        _window.Identity.Text = loggedIn
            ? Loc.GetString("stories-hermes-identity", ("name", _name), ("access", Loc.GetString($"stories-hermes-access-{_access}")))
            : Loc.GetString("stories-hermes-title");
        _window.Connection.Text = _connected ? string.Empty : Loc.GetString("stories-hermes-offline");
        if (!loggedIn || !EntMan.TryGetComponent(Owner, out HermesTerminalComponent? terminal))
            return;
        _window.Connection.Text = terminal.Connected ? string.Empty : Loc.GetString("stories-hermes-offline");
        _window.LogsTab.Visible = _window.LoginsTab.Visible = _access >= HermesAccess.Certified;
        _window.SecurityTab.Visible = _access >= HermesAccess.Joe;
        if (_tab is Page.Logs or Page.Logins && _access < HermesAccess.Certified ||
            _tab == Page.Security && _access < HermesAccess.Joe)
            _tab = Page.Maintenance;
        _window.MaintenanceTab.Selected = _tab == Page.Maintenance;
        _window.AccessTab.Selected = _tab == Page.Access;
        _window.LogsTab.Selected = _tab == Page.Logs;
        _window.LoginsTab.Selected = _tab == Page.Logins;
        _window.SecurityTab.Selected = _tab == Page.Security;
        _window.Submission.Visible = _tab == Page.Maintenance || _tab == Page.Access;
        _window.Category.Visible = _window.Priority.Visible = _tab == Page.Maintenance;
        _window.Priority.Disabled = _access < HermesAccess.Reporter;
        _window.SecurityPanel.Visible = _tab == Page.Security;
        _window.Entries.RemoveAllChildren();
        if (_tab is Page.Maintenance or Page.Access)
            RefreshTickets(terminal);
        else if (_tab is Page.Logs or Page.Logins)
            RefreshLogs();
        else
            RefreshSecurity();
        _window.Empty.Visible = _window.Entries.ChildCount == 0 && _tab != Page.Security;
        RefreshSubmission();
    }

    private void RefreshSubmission()
    {
        if (_window == null)
            return;
        var max = EntMan.TryGetComponent(Owner, out HermesTerminalComponent? terminal) ? terminal.MaxDetailLength : 512;
        var length = _window.Details.Text.Trim().Length;
        _window.Counter.Text = Loc.GetString("stories-hermes-counter", ("length", length), ("max", max));
        _window.Counter.Tone = length > max ? RMCCrtTone.Danger : RMCCrtTone.Muted;
        _window.Submit.Disabled = length == 0 || length > max || terminal?.Connected != true ||
            _tab == Page.Access && _access > HermesAccess.Reporter;
        _window.Submit.Text = Loc.GetString(_tab == Page.Access ? "stories-hermes-request-access" : "stories-hermes-submit");
    }

    private void Submit()
    {
        if (_window == null || _window.Submit.Disabled)
            return;
        SendMessage(new HermesActionMessage(_tab == Page.Access ? HermesAction.RequestAccess : HermesAction.Report,
            text: _window.Details.Text, category: (HermesCategory) _window.Category.SelectedId, priority: _window.Priority.Pressed));
    }

    private IEnumerable<T> Paginate<T>(List<T> entries)
    {
        var pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        _window!.PageNumber.Text = Loc.GetString("stories-hermes-page", ("page", _page + 1), ("pages", pages));
        _window.PreviousPage.Disabled = _page == 0;
        _window.NextPage.Disabled = _page + 1 >= pages;
        return entries.Skip(_page * PageSize).Take(PageSize);
    }

    private void RefreshTickets(HermesTerminalComponent terminal)
    {
        var tickets = terminal.Tickets.Where(t => t.AccessRequest == (_tab == Page.Access) &&
            (_window!.ShowClosed.Pressed || !SharedHermesSystem.IsClosed(t.Status)))
            .OrderByDescending(t => t.Priority).ThenByDescending(t => t.Id).ToList();
        foreach (var ticket in Paginate(tickets))
        {
            var row = new HermesTicketRow();
            row.Header.Text = Loc.GetString("stories-hermes-ticket-header", ("id", ticket.Id),
                ("category", Loc.GetString(ticket.AccessRequest ? "stories-hermes-access-tickets" : $"stories-hermes-category-{ticket.Category}")),
                ("status", Loc.GetString($"stories-hermes-status-{ticket.Status}")));
            row.Header.Tone = ticket.Priority ? RMCCrtTone.Warning : RMCCrtTone.Default;
            row.Submitter.Text = Loc.GetString("stories-hermes-submitter", ("name", ticket.Submitter), ("time", FormatTime(ticket.CreatedAt)));
            row.Details.Text = ticket.Details;
            row.Assignee.Text = Loc.GetString("stories-hermes-assignee", ("name", ticket.Assignee ?? Loc.GetString("stories-hermes-unassigned")));
            var manager = _access >= HermesAccess.Joe;
            var active = SharedHermesSystem.CanResolve(ticket.Status);
            var own = ticket.Card == _card;
            var assigned = ticket.AssigneeCard == null || ticket.AssigneeCard == _card;
            Bind(row.Claim, HermesAction.Claim, ticket.Id, manager && active,
                ticket.AssigneeCard != null && ticket.AssigneeCard != _card);
            row.Claim.Text = Loc.GetString(ticket.AssigneeCard == _card ? "stories-hermes-unclaim" : "stories-hermes-claim");
            Bind(row.Complete, HermesAction.Complete, ticket.Id, manager && active && assigned && !ticket.AccessRequest);
            Bind(row.Reject, HermesAction.Reject, ticket.Id, manager && active && assigned, true);
            Bind(row.Priority, HermesAction.Priority, ticket.Id, manager && active && !ticket.AccessRequest);
            row.Priority.Text = Loc.GetString(ticket.Priority
                ? "stories-hermes-remove-priority"
                : "stories-hermes-priority");
            Bind(row.Grant, HermesAction.Grant, ticket.Id, manager && active && assigned && ticket.AccessRequest, true);
            Bind(row.Revoke, HermesAction.Revoke, ticket.Id, manager && ticket.Status == HermesTicketStatus.Granted, true);
            Bind(row.Return, HermesAction.ReturnAccess, ticket.Id, own && ticket.Status == HermesTicketStatus.Granted, true);
            Bind(row.Cancel, HermesAction.Cancel, ticket.Id, own && active, true);
            _window!.Entries.AddChild(row);
        }
    }

    private void Bind(RMCCrtActionButton button, HermesAction action, int ticket, bool visible, bool confirm = false)
    {
        button.Visible = visible;
        button.OnPressed += _ =>
        {
            var message = new HermesActionMessage(action, ticket);
            if (confirm)
                Confirm(message, Loc.GetString("stories-hermes-confirm-ticket", ("id", ticket)));
            else
                SendMessage(message);
        };
    }

    private void RefreshLogs()
    {
        if (!EntMan.TryGetComponent(Owner, out HermesRestrictedUiComponent? data))
            return;
        var entries = new List<HermesLogEntry>(_tab == Page.Logins ? data.LoginLogs : data.Logs);
        entries.Reverse();
        foreach (var log in Paginate(entries))
        {
            var row = new HermesLogRow();
            row.Message.Text = Loc.GetString("stories-hermes-log-entry", ("time", FormatTime(log.Time)),
                ("author", log.Author), ("message", log.Message));
            _window!.Entries.AddChild(row);
        }
    }

    private void RefreshSecurity()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out HermesRestrictedUiComponent? data))
            return;
        _window.SecurityPanel.Refresh(new Content.Shared._Stories.ARES.Interface.AresSecurityView(
            data.LockedDown, data.LockdownReadyAt, data.Iff, data.DoorCount, data.SentryCount, data.Vents),
            _timing.CurTime, _access >= HermesAccess.Technician);
        Paginate(new List<int>());
    }

    private void Confirm(HermesActionMessage message, string text)
    {
        _confirmation?.Close();
        _confirmation = new HermesConfirmWindow();
        var dialog = _confirmation;
        dialog.Message.Text = text;
        dialog.Accept.OnPressed += _ => { SendMessage(message); dialog.Close(); };
        dialog.Cancel.OnPressed += _ => dialog.Close();
        dialog.OpenCentered();
    }

    private static string FormatTime(TimeSpan time) => $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}
