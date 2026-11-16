using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BulwarkPassiveComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BarbedDamageMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public int PassiveFrontalBonus = 10;

    [DataField, AutoNetworkedField]
    public int PassiveSideBonus = 10;
}
