using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Network;

namespace Content.Client._RMC14.TacticalMap;

/// <summary>
/// Keeps this viewer's private annotations across UI windows for the lifetime of their entity.
/// </summary>
[RegisterComponent]
[Access(typeof(TacticalMapSystem))]
public sealed partial class TacticalMapPersonalLabelsComponent : Component
{
    public readonly Dictionary<NetEntity, Dictionary<Vector2i, TacticalMapLabelData>> Labels = new();
}
