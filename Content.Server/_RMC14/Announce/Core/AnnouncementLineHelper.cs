using System.Text;
using Content.Shared._RMC14.Announce;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Announce.Core;

internal static class AnnouncementLineHelper
{
    internal static string[] NormalizeAndSplit(string message)
    {
        if (string.IsNullOrEmpty(message))
            return Array.Empty<string>();

        var normalized = message.Replace("\r\n", "\n").Replace('\r', '\n');
        if (!normalized.Contains('\n') && normalized.Contains("\\n"))
            normalized = normalized.Replace("\\n", "\n");

        var lines = normalized.Split('\n');
        var firstNonBlank = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (firstNonBlank < 0)
            return Array.Empty<string>();

        var lastNonBlank = Array.FindLastIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        return BalanceLineMarkup(lines[firstNonBlank..(lastNonBlank + 1)]);
    }

    private static string[] BalanceLineMarkup(string[] lines)
    {
        var openTags = new List<MarkupNode>();
        for (var i = 0; i < lines.Length; i++)
        {
            var builder = new StringBuilder();
            foreach (var tag in openTags)
                AnnouncementMarkup.AppendNode(builder, tag);

            foreach (var node in FormattedMessage.FromMarkupPermissive(lines[i]))
            {
                if (node.Name == null)
                {
                    AnnouncementMarkup.AppendNode(builder, node);
                }
                else if (!node.Closing)
                {
                    AnnouncementMarkup.AppendNode(builder, node);
                    openTags.Add(node);
                }
                else
                {
                    var matchingTag = openTags.FindLastIndex(tag => tag.Name == node.Name);
                    if (matchingTag < 0)
                        continue;

                    for (var j = openTags.Count - 1; j >= matchingTag; j--)
                    {
                        builder.Append("[/");
                        builder.Append(openTags[j].Name);
                        builder.Append(']');
                        openTags.RemoveAt(j);
                    }
                }
            }

            // Each label has its own drawing context, so close and reopen formatting at line boundaries.
            for (var j = openTags.Count - 1; j >= 0; j--)
            {
                builder.Append("[/");
                builder.Append(openTags[j].Name);
                builder.Append(']');
            }

            lines[i] = builder.ToString();
        }

        return lines;
    }
}
