using System.Text;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Announce;

public static class AnnouncementMarkup
{
    public static void AppendNode(StringBuilder builder, MarkupNode node)
    {
        if (node.Name == null)
        {
            builder.Append(FormattedMessage.EscapeText(node.Value.StringValue ?? string.Empty));
            return;
        }

        builder.Append('[');
        if (node.Closing)
            builder.Append('/');

        builder.Append(node.Name);
        builder.Append(node.Value.ToString().Replace("\r\n", "\\n").Replace("\r", "\\n").Replace("\n", "\\n"));
        foreach (var (name, value) in node.Attributes)
        {
            builder.Append(' ');
            builder.Append(name);
            builder.Append(value);
        }

        builder.Append(']');
    }
}
