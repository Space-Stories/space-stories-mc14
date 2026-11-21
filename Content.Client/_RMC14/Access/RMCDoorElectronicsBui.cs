using System.Linq;
using Content.Shared._RMC14.Access;
using Content.Shared._RMC14.UserInterface;
using Content.Shared.Doors.Electronics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Access;

public sealed class RMCDoorElectronicsBui(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey), IRefreshableBui
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private const int AnyMode = 0;
    private const int AllMode = 1;

    private RMCDoorElectronicsWindow? _window;

    protected override void Open()
    {
        base.Open();
        if (!EntMan.TryGetComponent(Owner, out RMCDoorElectronicsComponent? electronics))
            return;

        var window = this.CreateWindow<RMCDoorElectronicsWindow>();
        _window = window;
        window.FitToViewport();
        window.Title = Loc.GetString("rmc-door-electronics-title", ("board", EntMan.GetComponent<MetaDataComponent>(Owner).EntityName));

        window.Mode.AddItem(Loc.GetString("rmc-door-electronics-mode-any"), AnyMode);
        window.Mode.AddItem(Loc.GetString("rmc-door-electronics-mode-all"), AllMode);
        window.Mode.OnItemSelected += args => SendMessage(new RMCDoorElectronicsSetModeMessage(args.Id == AllMode));
        window.ResetAccesses.OnPressed += _ => SendMessage(new DoorElectronicsUpdateConfigurationMessage([]));

        var allowed = _prototypes.Index(electronics.AccessCatalog).Tags
            .OrderBy(access => _prototypes.Index(access).GetAccessLevelName())
            .ToList();
        window.AccessLevels.Populate(allowed, _prototypes);
        window.FitAccessButtons();
        foreach (var button in window.AccessLevels.ButtonsList.Values)
        {
            button.OnPressed += _ =>
            {
                var selected = window.AccessLevels.ButtonsList.Where(pair => pair.Value.Pressed).Select(pair => pair.Key);
                SendMessage(new DoorElectronicsUpdateConfigurationMessage(selected.ToList()));
            };
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { } window || !EntMan.TryGetComponent(Owner, out RMCDoorElectronicsComponent? electronics))
            return;

        var canEdit = !electronics.Locked;
        var lastOperator = electronics.LastOperator;
        var operatorText = string.IsNullOrWhiteSpace(lastOperator)
            ? null
            : Loc.GetString("rmc-door-electronics-last-operator", ("name", lastOperator));
        window.LastOperator.Text = operatorText;
        window.LastOperator.Visible = operatorText is not null;
        var hint = electronics.Locked ? "rmc-door-electronics-id-required" : "rmc-door-electronics-id-to-lock";
        window.CardHint.Text = Loc.GetString(hint);
        window.Mode.SelectId(electronics.RequireAll ? AllMode : AnyMode);
        window.Mode.Disabled = !canEdit;
        window.ResetAccesses.Disabled = !canEdit || electronics.SelectedAccesses.Count == 0;
        window.AccessLevels.UpdateState(electronics.SelectedAccesses, canEdit ? null : []);
    }
}
