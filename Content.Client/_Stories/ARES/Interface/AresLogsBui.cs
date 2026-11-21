using System.Linq;
using Content.Client._RMC14.ARES;
using Content.Shared._RMC14.ARES.ExternalTerminals;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.UserInterface;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Stories.ARES.Interface;

[UsedImplicitly]
public sealed class AresLogsBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey), IRefreshableBui
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private static readonly ISawmill Log = Logger.GetSawmill("ares-logs-ui");
    private const int PageSize = 12;
    private ARESExternalTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ARESExternalTerminalWindow>();
        _window.ConfigureLogViewer();
        _window.SignIn.OnPressed += _ => SendMessage(new AresActionMessage(AresAction.Login));
        _window.LogOut.OnPressed += _ => SendMessage(new AresActionMessage(AresAction.Logout));
        _window.Home.OnPressed += _ => SendMessage(new RMCARESExternalShowLogs(null, 0));
        _window.Back.OnPressed += _ => SendMessage(new RMCARESExternalShowLogs(null, 0));
        _window.LogLeft.OnPressed += _ => TurnPage(-1);
        _window.LogRight.OnPressed += _ => TurnPage(1);
        Refresh();
    }

    private void TurnPage(int offset)
    {
        if (EntMan.TryGetComponent(Owner, out AresTerminalComponent? data) && data.SelectedLog is { } type)
            SendMessage(new RMCARESExternalShowLogs(type, data.LogIndex + offset));
    }

    public void Refresh()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out AresTerminalComponent? data))
            return;
        try
        {
            var loggedIn = data.Access != AresAccess.LoggedOut && data.Connected;
            _window.LoginScreen.Visible = !loggedIn;
            _window.MainProgram.Visible = loggedIn;
            _window.LogCategory.ButtonContainer.RemoveAllChildren();
            _window.LogsContainer.RemoveAllChildren();
            if (!loggedIn)
            {
                _window.SignedInUser.Text = string.Empty;
                return;
            }
            _window.SignedInUser.Text = $"[font size=15]{FormattedMessage.EscapeText(data.User)}[/font]";
            _window.HomeMenu.Visible = data.SelectedLog == null;
            _window.LogMenu.Visible = data.SelectedLog != null;
            _window.Back.Disabled = data.SelectedLog == null;
            _window.LogCategory.Visible = true;
            _window.LogCategory.SectionNameText = Loc.GetString(data.AvailableLogs.Count == 0
                ? "stories-hermes-empty" : "stories-ares-open-logs");
            foreach (var type in data.AvailableLogs.OrderBy(t => _prototypes.Index(t).Name))
            {
                var prototype = _prototypes.Index(type);
                var button = new ARESExternalTerminalButton();
                button.logButton.Text = prototype.Name;
                if (prototype.TryGetComponent<ARESLogTypeComponent>(out var component, _componentFactory))
                    button.ModulateSelfOverride = component.Color;
                button.logButton.OnPressed += _ => SendMessage(new RMCARESExternalShowLogs(type, 0));
                _window.LogCategory.ButtonContainer.AddChild(button);
            }
            if (data.SelectedLog is not { } selected || !_prototypes.TryIndex(selected, out var category))
                return;
            _window.LogsName.Text = $"[font size=16]{FormattedMessage.EscapeText(category.Name)}[/font]";
            _window.LogsDescription.Text = $"[font size=12]{FormattedMessage.EscapeText(category.Description)}[/font]";
            _window.LogsDescription.Visible = !string.IsNullOrWhiteSpace(category.Description);
            var pages = Math.Max(1, (data.LogCount + PageSize - 1) / PageSize);
            _window.LogNumber.Text = $"[font size=16]{data.LogIndex + 1}/{pages}[/font]";
            _window.LogLeft.Disabled = data.LogIndex == 0;
            _window.LogRight.Disabled = data.LogIndex + 1 >= pages;
            foreach (var entry in data.LogEntries)
                AddLogRow(entry);
            if (data.LogEntries.Count == 0)
                AddLogRow(Loc.GetString("stories-hermes-empty"));
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to refresh classic ARES logs: {exception}");
        }
    }

    private void AddLogRow(string text)
    {
        var row = new AresLegacyLogRow();
        row.SetText(text);
        _window!.LogsContainer.AddChild(row);
    }
}
