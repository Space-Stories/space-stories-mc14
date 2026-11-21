using Content.Shared._Stories.Xenonids.Predalien.ToggleGutTargeting;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralFrenzy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STFeralFrenzySystem), typeof(STToggleGutTargetingSystem))]
public sealed partial class STFeralFrenzyComponent : Component
{
    [DataField, AutoNetworkedField]
    public STFeralFrenzyTargeting Targeting = STFeralFrenzyTargeting.Single;

    [DataField, AutoNetworkedField]
    public TimeSpan SingleCastTime = TimeSpan.FromSeconds(0.5);

    [DataField, AutoNetworkedField]
    public float SingleBaseDamage = 25f;

    [DataField, AutoNetworkedField]
    public float SingleDamagePerKill = 10f;

    [DataField, AutoNetworkedField]
    public float SingleRange = 1.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan AoeCastTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public float AoeBaseDamage = 15f;

    [DataField, AutoNetworkedField]
    public float AoeDamagePerKill = 10f;

    [DataField, AutoNetworkedField]
    public float AoeRange = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(0.5);

    [DataField, AutoNetworkedField]
    public SoundSpecifier SingleSound = new SoundPathSpecifier("/Audio/_Stories/Voice/Predalien/predalien_growl.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier AoeSound = new SoundPathSpecifier("/Audio/_Stories/Voice/Predalien/predalien_death.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId GoreEffect = "RMCEffectTailHit";
}

[Serializable, NetSerializable]
public enum STFeralFrenzyTargeting : byte
{
    Single,
    Aoe,
}
