using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.TailSweep;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BulwarkTailSweepComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Damage = 15;

    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    [DataField, AutoNetworkedField]
    public float GrenadeKickRange = 3f;

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(0.2);

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(11);

    [DataField, AutoNetworkedField]
    public TimeSpan GrenadeCooldown = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "CMEffectPunch";

    [DataField, AutoNetworkedField]
    public SoundSpecifier SwingSound = new SoundPathSpecifier("/Audio/_Stories/Xeno/sound_effects_tail_swing.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/_Stories/Xeno/sound_effects_tail_swing.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier GrenadeKickSound = new SoundPathSpecifier("/Audio/_Stories/Xeno/sound_effects_grenade_hit.ogg");
}
