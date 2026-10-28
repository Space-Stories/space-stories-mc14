using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._Stories.Hunter;

public enum PlasmaGunSwitch
{
    Lethal,
    Incendiary,
}

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class GunSwitchComponent : Component
{
    [DataField] [AutoNetworkedField]
    public EntProtoId LethalBolt = "STPistolBolt";

    [DataField]
    public EntProtoId IncendiaryBolt = "STPistolBoltIncendiary";

    [DataField] [AutoNetworkedField]
    public PlasmaGunSwitch Mode = PlasmaGunSwitch.Lethal;
}
