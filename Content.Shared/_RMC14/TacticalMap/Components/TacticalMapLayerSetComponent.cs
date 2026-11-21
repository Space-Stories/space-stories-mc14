using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent]
public sealed partial class TacticalMapLayerSetComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId<TacticalMapLayerComponent>> Layers { get; private set; } = new();
}
