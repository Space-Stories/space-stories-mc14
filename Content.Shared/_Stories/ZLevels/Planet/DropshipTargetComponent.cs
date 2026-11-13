using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Dropship.Weapon;

public sealed partial class DropshipTargetComponent
{
    [AutoNetworkedField]
    public EntityUid? TargetMap;
}
