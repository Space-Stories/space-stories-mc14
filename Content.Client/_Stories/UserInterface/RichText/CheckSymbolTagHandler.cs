using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Stories.UserInterface.RichText;

public sealed class CheckSymbolTagHandler : IMarkupTagHandler
{
    public string Name => "checksym";

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public string TextBefore(MarkupNode node) => "";
    public string TextAfter(MarkupNode node) => "";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        var symbol = node.Value.StringValue;
        if (symbol is not ("☐" or "✔" or "✖"))
        {
            control = null;
            return false;
        }

        control = new Label
        {
            Text = symbol,
            FontColorOverride = new Color(25, 25, 25),
        };
        return true;
    }
}
