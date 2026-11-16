using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

public enum PlasmaCasterMode
{
    Stun,
    Immobilizer,
    Lethal,
    Overcharge,
}

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class PlasmaCasterComponent : Component
{
    [DataField] [AutoNetworkedField]
    public NetEntity? Bracer;

    [DataField]
    public EntProtoId ImmobilizerAmmo = "STPlasmaBoltImmobilizerProjectile";

    [DataField] [ViewVariables(VVAccess.ReadWrite)]
    public float ImmobilizerFireRate = 0.125f;

    [DataField]
    public SoundSpecifier ImmobilizerSound = new SoundPathSpecifier("/Audio/_Stories/Weapons/pulse.ogg");

    [DataField]
    public EntProtoId LethalAmmo = "STPlasmaBoltLethalProjectile";

    [DataField] [ViewVariables(VVAccess.ReadWrite)]
    public float LethalFireRate = 0.56f;

    [DataField]
    public SoundSpecifier LethalSound = new SoundPathSpecifier("/Audio/_Stories/Weapons/pred_lasercannon.ogg");

    [DataField] [AutoNetworkedField]
    public PlasmaCasterMode Mode = PlasmaCasterMode.Stun;

    [DataField]
    public EntProtoId OverchargeAmmo = "STPlasmaBoltOverchargeProjectile";

    [DataField] [ViewVariables(VVAccess.ReadWrite)]
    public float OverchargeFireRate = 0.083f;

    [DataField]
    public SoundSpecifier OverchargeSound = new SoundPathSpecifier("/Audio/_Stories/Weapons/pred_plasma_shot.ogg");

    [DataField]
    public EntProtoId StunAmmo = "STPlasmaBoltStunProjectile";

    [DataField] [ViewVariables(VVAccess.ReadWrite)]
    public float StunFireRate = 1.67f;

    [DataField]
    public SoundSpecifier StunSound = new SoundPathSpecifier("/Audio/_Stories/Weapons/pred_plasmacaster_fire.ogg");
}
