using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Predalien.PredalienRoar;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STPredalienRoarSystem))]
public sealed partial class STPredalienRoarComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 7f;

    [DataField, AutoNetworkedField]
    public TimeSpan BaseDuration = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan DurationPerKill = TimeSpan.FromSeconds(0.25);

    [DataField, AutoNetworkedField]
    public float BonusDamagePerKill = 2.5f;

    [DataField, AutoNetworkedField]
    public float BonusSpeedPerKill = 0.05f;

    [DataField, AutoNetworkedField]
    public TimeSpan HunterRecloakPenalty = TimeSpan.FromSeconds(2.5);

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Stories/Voice/Predalien/predalien_roar.ogg");
}
