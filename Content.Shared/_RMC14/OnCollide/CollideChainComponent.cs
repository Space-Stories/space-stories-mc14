using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.OnCollide;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedOnCollideSystem))]
public sealed partial class CollideChainComponent : Component
{
    // Stories-DeletedEntityReferences-Start
    // Keep hit identity without resolving victims again during state serialization.
    [DataField, AutoNetworkedField]
    public HashSet<NetEntity> Hit = new();
    // Stories-DeletedEntityReferences-End
}
