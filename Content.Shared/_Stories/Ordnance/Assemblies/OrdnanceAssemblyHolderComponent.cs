using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Ordnance.Assemblies;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class OrdnanceAssemblyHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Part1;

    [DataField, AutoNetworkedField]
    public EntityUid? Part2;

    [DataField, AutoNetworkedField]
    public bool IsLocked = true;
    
    [DataField]
    public Container? Container;
}
