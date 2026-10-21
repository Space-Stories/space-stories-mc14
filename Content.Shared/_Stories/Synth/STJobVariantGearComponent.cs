using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth;

[RegisterComponent]
public sealed partial class STJobVariantGearComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, List<EntProtoId>> Variants = new();

    [DataField]
    public string Slot = "jumpsuit";

    [DataField]
    public string[] DependentSlots = { "pocket1", "pocket2", "belt" };
}

public readonly record struct STJobVariantGearAppliedEvent(string Variant);
