using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralRush;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STFeralRushingComponent : Component
{
    [DataField, AutoNetworkedField]
    public int ArmorGain;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1f;

    [DataField, AutoNetworkedField]
    public TimeSpan SpeedExpireAt;

    [DataField, AutoNetworkedField]
    public bool SpeedExpired;

    [DataField, AutoNetworkedField]
    public TimeSpan ArmorExpireAt;
}
