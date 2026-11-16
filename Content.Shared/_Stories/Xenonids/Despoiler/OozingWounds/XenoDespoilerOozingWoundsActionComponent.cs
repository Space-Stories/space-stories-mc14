using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.Despoiler;

[RegisterComponent]
public sealed partial class XenoDespoilerOozingWoundsActionComponent : Component
{
    [DataField]
    public int BaseRadius = 1;

    [DataField]
    public float SeverityHpThreshold1 = 0.7f;

    [DataField]
    public float SeverityHpThreshold2 = 0.3f;

    [DataField]
    public float LingeringAcidChance = 0.2f;

    [DataField]
    public TimeSpan DistanceDelayPerTile = TimeSpan.FromSeconds(0.2);

    [DataField]
    public EntProtoId TelegraphProto = "STEffectDespoilerOozingTelegraph";

    [DataField]
    public EntProtoId AcidSprayProto = "STEffectDespoilerAcidSpray";

    [DataField]
    public EntProtoId AcidSprayEmpoweredProto = "STEffectDespoilerAcidSprayEmpowered";

    [DataField]
    public EntProtoId LingeringAcidProto = "STEffectDespoilerLingeringAcid";

    [DataField]
    public SoundSpecifier? CastSound;
}
