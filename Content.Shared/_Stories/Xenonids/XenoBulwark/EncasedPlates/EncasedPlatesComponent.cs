using Content.Shared._RMC14.Stun;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.EncasedPlates;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EncasedPlatesComponent : Component
{
    [DataField, AutoNetworkedField]
    public int FrontalArmorBonus = 10;

    [DataField, AutoNetworkedField]
    public int SideArmorBonus = -10;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 0.55f;

    [DataField, AutoNetworkedField]
    public float DamageModifier = -8f;

    [DataField, AutoNetworkedField]
    public string[] ImmuneToStatuses = { "KnockedDown" };

    [DataField, AutoNetworkedField]
    public RMCSizes ActiveSize = RMCSizes.Big;

    [DataField, AutoNetworkedField]
    public RMCSizes? OriginalSize;

    [DataField, AutoNetworkedField]
    public bool Active;
}
