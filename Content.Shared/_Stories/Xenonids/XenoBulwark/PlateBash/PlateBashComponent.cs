using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.PlateBash;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlateBashComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Damage = 20;

    [DataField, AutoNetworkedField]
    public float RangeNormal = 3f;

    [DataField, AutoNetworkedField]
    public float RangeEncased = 2f;

    [DataField, AutoNetworkedField]
    public float KnockbackNormal = 1f;

    [DataField, AutoNetworkedField]
    public float KnockbackEncased = 3f;

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(0.2);

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "CMEffectPunch";

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");

    [DataField, AutoNetworkedField]
    public bool IsCharging;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField, AutoNetworkedField]
    public Vector2? Charge;
}
