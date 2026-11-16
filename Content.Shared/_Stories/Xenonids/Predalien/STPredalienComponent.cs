using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Predalien;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STPredalienSystem))]
public sealed partial class STPredalienComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Kills;

    [DataField]
    public int MaxKills = 10;

    [DataField]
    public float DamagePerKill = 2.5f;

    [DataField]
    public float HunterDamageMultiplier = 1.5f;

    [DataField]
    public bool AnnouncedToHunters;
}
