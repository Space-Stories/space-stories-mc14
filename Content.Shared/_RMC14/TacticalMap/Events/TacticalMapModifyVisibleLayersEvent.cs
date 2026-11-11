using System.Collections.Generic;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.TacticalMap;

[ByRefEvent]
public readonly record struct TacticalMapModifyVisibleLayersEvent(
    EntityUid? Viewer,
    HashSet<EntProtoId<TacticalMapLayerComponent>> Layers);
