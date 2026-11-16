using Content.Shared._RMC14.SupplyDrop;
using Content.Shared._Stories.ZLevels.Planet; // Stories-ZLevels
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RMC14.SupplyDrop;

[UsedImplicitly]
public sealed class SupplyDropComputerBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SupplyDropWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SupplyDropWindow>();
        _window.Longitude.OnValueChanged +=
            args => SendPredictedMessage(new SupplyDropComputerLongitudeBuiMsg((int)args.Value));
        _window.Latitude.OnValueChanged +=
            args => SendPredictedMessage(new SupplyDropComputerLatitudeBuiMsg((int)args.Value));
        // Stories-ZLevels-Start
        _window.ZLevel.ValueChanged +=
            args => SendPredictedMessage(new SupplyDropComputerZLevelBuiMsg(args.Value));
        // Stories-ZLevels-End
        _window.LaunchButton.OnPressed += _ => SendPredictedMessage(new SupplyDropComputerLaunchBuiMsg());
        _window.UpdateButton.OnPressed += _ => SendPredictedMessage(new SupplyDropComputerUpdateBuiMsg());

        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out SupplyDropComputerComponent? supplyDrop))
            return;

        _window.Longitude.Value = supplyDrop.Coordinates.X;
        _window.Latitude.Value = supplyDrop.Coordinates.Y;
        _window.ZLevel.OverrideValue(supplyDrop.TargetZ); // Stories-ZLevels
        _window.LastUpdateAt = supplyDrop.LastLaunchAt;
        _window.NextUpdateAt = supplyDrop.NextLaunchAt;
        _window.CrateStatusLabel.Text = Loc.GetString("ui-supply-drop-crate-status", ("hasCrate", supplyDrop.HasCrate));

    }
}
