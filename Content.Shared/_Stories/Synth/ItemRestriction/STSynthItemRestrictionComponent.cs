using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STSynthItemRestrictionSystem))]
public sealed partial class STSynthItemRestrictionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool SynthOnly = true;

    [DataField, AutoNetworkedField]
    public bool CheckPickup = true;

    [DataField, AutoNetworkedField]
    public bool CheckEquip = true;

    [DataField, AutoNetworkedField]
    public bool CheckUse = true;

    [DataField, AutoNetworkedField]
    public LocId DenyPopup = "st-synth-item-restricted";
}
