using System.Linq;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Requisitions;

public sealed class RequisitionPresetSelector : OptionButton
{
    public RequisitionPresetSelector()
    {
        foreach (var label in Children.SelectMany(child => child.Children).OfType<Label>())
            label.ClipText = true;
        OptionsScroll.HScrollEnabled = false;
    }

    public override void ButtonOverride(Button button)
    {
        button.ClipText = true;
        button.HorizontalExpand = true;
    }

    protected override void Resized()
    {
        base.Resized();
        OptionsScroll.MinWidth = Width;
        OptionsScroll.MaxWidth = Width;
    }
}
