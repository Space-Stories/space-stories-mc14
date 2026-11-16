using Content.Shared._RMC14.MilitaryPolice;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RMC14.MilitaryPolice;

[UsedImplicitly]
public sealed class RMCPoliceConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private RMCPoliceRecordsWindow? _records;
    private RMCSentencingWindow? _sentencing;

    protected override void Open()
    {
        base.Open();
        if (EntMan.TryGetComponent(Owner, out RMCPoliceConsoleComponent? console) && console.Sentencing)
        {
            _sentencing = this.CreateWindow<RMCSentencingWindow>();
            _sentencing.Message += SendMessage;
        }
        else
        {
            _records = this.CreateWindow<RMCPoliceRecordsWindow>();
            _records.Message += SendMessage;
        }
        Refresh();
    }

    public void Refresh()
    {
        if (!EntMan.TryGetComponent(Owner, out RMCPoliceUiComponent? component) || component.Data is not { } police)
            return;
        _records?.Update(police);
        _sentencing?.Update(police);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is RMCPoliceRecordSelectedMsg selected)
            _records?.SelectRecord(selected.Id);
    }
}
