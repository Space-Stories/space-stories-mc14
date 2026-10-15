using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.MaskKnockoff;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STXenoMaskKnockoffSystem))]
public sealed partial class STXenoMaskKnockoffComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BaseChance = 0.5f;

    [DataField, AutoNetworkedField]
    public float FrenzyChancePerMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float IntelligentCasteBonusChance = 1f;

    [DataField, AutoNetworkedField]
    public float DamageChanceDivisor = 8f;

    [DataField, AutoNetworkedField]
    public float MaxDamageBonusChance = 5f;

    [DataField, AutoNetworkedField]
    public float IncapacitatedChance = 35f;

    [DataField, AutoNetworkedField]
    public float MaxChance = 8f;
}
