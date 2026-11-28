using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Network;

namespace Content.Client._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    public IReadOnlyDictionary<Vector2i, TacticalMapLabelData>? GetPersonalLabels(EntityUid? viewer, NetEntity map)
    {
        return TryComp(viewer, out TacticalMapPersonalLabelsComponent? personal)
            ? personal.Labels.GetValueOrDefault(map)
            : null;
    }

    public void SetPersonalLabel(EntityUid? viewer, NetEntity map, Vector2i position, TacticalMapLabelData? label)
    {
        if (viewer == null || !TryGetEntity(map, out var mapEntity) ||
            !TryComp(mapEntity, out TacticalMapComponent? tactical))
        {
            return;
        }

        var personal = EnsureComp<TacticalMapPersonalLabelsComponent>(viewer.Value);
        if (!personal.Labels.TryGetValue(map, out var labels))
        {
            if (label == null)
                return;

            labels = new Dictionary<Vector2i, TacticalMapLabelData>();
            personal.Labels.Add(map, labels);
        }

        if (label is not { } data || string.IsNullOrWhiteSpace(data.Text))
        {
            labels.Remove(position);
            if (labels.Count == 0)
                personal.Labels.Remove(map);
            return;
        }

        if (labels.Count >= tactical.MaxCanvasLabels && !labels.ContainsKey(position))
            return;

        var length = Math.Min(data.Text.Length, Math.Max(1, tactical.MaxLabelLength));
        labels[position] = data with { Text = data.Text[..length] };
    }
}
