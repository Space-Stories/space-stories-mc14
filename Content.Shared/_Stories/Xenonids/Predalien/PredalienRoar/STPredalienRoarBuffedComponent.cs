using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Predalien.PredalienRoar;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STPredalienRoarBuffedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BonusDamage;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1f;

    [DataField, AutoNetworkedField]
    public TimeSpan ExpireAt;
}
