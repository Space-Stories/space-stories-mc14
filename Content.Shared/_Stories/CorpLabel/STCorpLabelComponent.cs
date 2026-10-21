using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.CorpLabel;

[RegisterComponent]
public sealed partial class STCorpLabelComponent : Component
{
    [DataField(required: true)]
    public LocId Manufacturer;
}
