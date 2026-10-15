using Content.Shared._RMC14.Synth;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth;

[RegisterComponent]
public sealed partial class STJobVariantGenerationComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, EntProtoId<SynthGenerationComponent>> Variants = new();

    [ViewVariables]
    public bool SpawnCompleted;
}
