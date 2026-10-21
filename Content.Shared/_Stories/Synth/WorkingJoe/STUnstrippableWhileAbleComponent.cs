using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Synth.WorkingJoe;

[RegisterComponent, NetworkedComponent, Access(typeof(StoriesUnstrippableWhileAbleSystem))]
public sealed partial class STUnstrippableWhileAbleComponent : Component
{
    [DataField]
    public LocId UnequipReason = "stories-unstrippable-while-able-unequip";

    [DataField]
    public LocId EquipReason = "stories-unstrippable-while-able-equip";
}
