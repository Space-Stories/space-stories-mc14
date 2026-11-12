using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Weeds;

[RegisterComponent] // Stories-DeletedEntityReferences
[Access(typeof(SharedXenoWeedsSystem))]
public sealed partial class XenoWallWeedsComponent : Component
{
    [DataField] // Stories-DeletedEntityReferences
    public EntityUid? Weeds;
}
