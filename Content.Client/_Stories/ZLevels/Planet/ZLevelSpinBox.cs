using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stories.ZLevels.Planet;

public sealed class ZLevelSpinBox : SpinBox
{
    public ZLevelSpinBox()
    {
        // Allow the minus sign to be entered before the digits of an underground level.
        LineEditControl.IsValid = text => text is "" or "-" || int.TryParse(text, out _);
    }
}
