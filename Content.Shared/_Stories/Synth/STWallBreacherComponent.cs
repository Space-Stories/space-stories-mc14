using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STWallBreacherComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan ReinforcedDuration = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public FixedPoint2 Damage = 9999;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? FinishSound = new SoundCollectionSpecifier("MetalBreak");
}

[Serializable, NetSerializable]
public sealed partial class STWallBreachDoAfterEvent : SimpleDoAfterEvent;
