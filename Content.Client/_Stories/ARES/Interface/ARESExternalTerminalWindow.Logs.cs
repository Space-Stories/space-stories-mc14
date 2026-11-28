using System.Numerics;
using Robust.Client.UserInterface.Controls;

// ReSharper disable CheckNamespace
namespace Content.Client._RMC14.ARES;

public sealed partial class ARESExternalTerminalWindow
{
    public void ConfigureLogViewer()
    {
        Title = Loc.GetString("stories-ares-log-viewer-title");
        MinSize = new Vector2(560, 420);
        SetSize = new Vector2(900, 650);
        if (LogsContainer.Parent is ScrollContainer scroll)
            scroll.MaxWidth = float.PositiveInfinity;
        LogsContainer.HorizontalExpand = true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsNaN(SetWidth) ? availableSize.X : Math.Min(SetWidth, availableSize.X);
        if (!float.IsFinite(width))
            width = 900;
        var columns = Math.Clamp((int) ((width - 40) / 205), 1, 4);
        if (LogCategory.ButtonContainer.Columns != columns)
            LogCategory.ButtonContainer.Columns = columns;
        return base.MeasureOverride(availableSize);
    }
}
