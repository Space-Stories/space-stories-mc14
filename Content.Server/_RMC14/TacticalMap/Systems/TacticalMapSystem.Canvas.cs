using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Maths;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    private void SanitizeCanvas(Entity<TacticalMapComponent> map,
        ref List<TacticalMapLine> lines,
        ref Dictionary<Vector2i, TacticalMapLabelData> labels)
    {
        var validLines = new List<TacticalMapLine>();
        var validLabels = new Dictionary<Vector2i, TacticalMapLabelData>();
        foreach (var line in lines)
        {
            if (validLines.Count >= LineLimit)
                break;

            if (!float.IsFinite(line.Start.X) || !float.IsFinite(line.Start.Y) ||
                !float.IsFinite(line.End.X) || !float.IsFinite(line.End.Y) ||
                !float.IsFinite(line.Thickness))
            {
                continue;
            }

            validLines.Add(line with { Thickness = Math.Clamp(line.Thickness, 1f, Math.Max(1f, map.Comp.MaxLineThickness)) });
        }

        foreach (var (position, label) in labels)
        {
            if (validLabels.Count >= map.Comp.MaxCanvasLabels)
                break;

            if (string.IsNullOrWhiteSpace(label.Text))
            {
                continue;
            }

            var length = Math.Min(label.Text.Length, Math.Max(1, map.Comp.MaxLabelLength));
            validLabels.Add(position, label with { Text = label.Text[..length] });
        }

        lines = validLines;
        labels = validLabels;
    }
}
