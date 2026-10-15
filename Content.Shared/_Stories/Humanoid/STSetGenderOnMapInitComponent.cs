using Content.Shared._RMC14.Humanoid;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Humanoid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCHumanoidAppearanceSystem))]
public sealed partial class STSetGenderOnMapInitComponent : Component
{
    [DataField, AutoNetworkedField]
    public Gender Gender = Gender.Epicene;
}
