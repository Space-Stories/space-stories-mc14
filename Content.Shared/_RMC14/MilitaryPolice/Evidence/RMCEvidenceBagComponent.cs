using Content.Shared.Item;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.MilitaryPolice.Evidence;

/// <summary>
/// Holds one piece of evidence without handling it during collection.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCEvidenceBagSystem))]
public sealed partial class RMCEvidenceBagComponent : Component
{
    [DataField]
    public string ContainerId = "rmc-evidence-bag";

    [DataField, AutoNetworkedField]
    public ProtoId<ItemSizePrototype> MaxItemSize = "Normal";

    [DataField, AutoNetworkedField]
    public ProtoId<ItemSizePrototype> EmptySize = "Small";

    [ViewVariables]
    public ContainerSlot Container = default!;
}
