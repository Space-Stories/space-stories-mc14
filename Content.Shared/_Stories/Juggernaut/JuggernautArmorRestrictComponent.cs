using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Blocks equipping into the given slots while worn, and vice versa. Belt only - backpack/webbing use RMC's own ClothingBlockBackpack/ClothingBlockWebbing.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(JuggernautArmorRestrictSystem))]
public sealed partial class JuggernautArmorRestrictComponent : Component
{
    [DataField]
    public SlotFlags Slots = SlotFlags.BELT;

    [DataField]
    public LocId BlockOthersPopup = "st-juggernaut-armor-block-others";

    [DataField]
    public LocId BlockSelfPopup = "st-juggernaut-armor-block-self";
}
