using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class TacticalMapActiveLayerLabelsComponent : Component
{
    [AutoNetworkedField]
    public Guid UpdateId;

    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, TacticalMapLabelData> Labels = new();
}
