using System.Linq;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;
using Content.Shared.StationRecords;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._RMC14.MilitaryPolice;

[UsedImplicitly]
public sealed class RMCBrigCellBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPlayerManager _players = default!;

    private RMCBrigCellWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RMCBrigCellWindow>();
        _window.Message += SendMessage;
        _window.CanPardon = CanPardon;
        Refresh();
    }

    public void Refresh()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out RMCBrigCellUiComponent? component) ||
            component.Data is not { } cell)
            return;
        _window.Update(cell, CanPardon());
    }

    private bool CanPardon()
    {
        if (_players.LocalEntity is not { } actor ||
            !EntMan.TryGetComponent(Owner, out RMCBrigCellComponent? controller) ||
            !EntMan.System<AccessReaderSystem>().FindAccessTags(actor).Any(controller.PardonAccess.Contains))
            return false;

        if (!EntMan.TryGetComponent(Owner, out RMCBrigCellUiComponent? component) || component.Data is not { } state)
            return true;
        var report = state.Reports.FirstOrDefault(view => view.Paper == state.ViewedReport)?.Report;
        if (report?.RecordId is { } id && report.RecordStation is { } station &&
            EntMan.System<InventorySystem>().TryGetSlotEntity(actor, "id", out var card) &&
            EntMan.TryGetComponent(card, out StationRecordKeyStorageComponent? record) && record.Key is { } key)
            return key.Id != id || EntMan.GetNetEntity(key.OriginStation) != station;

        return report == null || !EntMan.TryGetComponent(actor, out MetaDataComponent? metadata) ||
               !string.Equals(metadata.EntityName, report.Suspect, StringComparison.Ordinal);
    }
}
