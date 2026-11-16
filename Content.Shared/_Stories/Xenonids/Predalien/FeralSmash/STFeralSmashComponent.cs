using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralSmash;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STFeralSmashSystem))]
public sealed partial class STFeralSmashComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DamagePerKill = 10f;
}
