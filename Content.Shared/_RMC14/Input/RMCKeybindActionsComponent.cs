using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Input;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCKeybindActionsComponent : Component
{
    // Stories-KeybindActions-Start
    public static readonly Dictionary<RMCKeybindActionSlot, List<EntProtoId>> DefaultXenoActions = new()
    {
        [RMCKeybindActionSlot.XenoCorrosiveAcid] = new()
        {
            "ActionXenoAcidWeak",
            "ActionXenoAcidNormal",
            "ActionXenoAcidStrong",
            "ActionXenoAcidAcider",
        },
        [RMCKeybindActionSlot.XenoPheromones] = new()
        {
            "ActionXenoPheromones",
        },
        [RMCKeybindActionSlot.XenoScreech] = new()
        {
            "ActionXenoScreech",
        },
        [RMCKeybindActionSlot.XenoWordQueen] = new()
        {
            "ActionXenoWordQueen",
        },
        [RMCKeybindActionSlot.XenoTailStab] = new()
        {
            "ActionXenoTailStab",
            "ActionXenoTailStabCorrosive",
            "ActionXenoTailStabBoiler",
            "ActionXenoTailSlam",
            "ActionXenoTailFountain",
            "ActionXenoTailStabLance",
            "ActionXenoTailSeize",
        },
        [RMCKeybindActionSlot.XenoHide] = new()
        {
            "ActionXenoHide",
        },
        [RMCKeybindActionSlot.XenoEvolve] = new()
        {
            "ActionXenoEvolve",
        },
    };
    // Stories-KeybindActions-End

    /// <summary>
    /// Action prototypes accepted by each semantic keybind, in priority order.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<RMCKeybindActionSlot, List<EntProtoId>> Actions = new();
}
