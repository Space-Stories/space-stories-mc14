using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Hunter.Traps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(HunterTrapSystem))]
public sealed partial class HunterTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Armable = true;

    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField, AutoNetworkedField]
    public EntityUid? Victim;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? ReleaseAt;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ArmDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public int Range = 5;

    [DataField]
    public int MinRange = 2;

    [DataField]
    public int MaxRange = 7;

    [DataField]
    public bool Configurable;

    [DataField]
    public string TetherVisualState = "chain";

    [DataField]
    public float Interference = 100f;

    [DataField]
    public bool Announce;

    [DataField]
    public DamageSpecifier? AnimalDamage;

    [DataField]
    public SoundSpecifier? CatchSound;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> Skill = "STSkillIllegalTechnology";
}
