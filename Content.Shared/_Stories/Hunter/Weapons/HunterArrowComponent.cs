using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HunterArrowSystem))]
public sealed partial class HunterArrowComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public ComponentRegistry? ActiveComponents;

    [DataField]
    public ComponentRegistry? InactiveComponents;

    [DataField]
    public LocId? ActiveName;

    [DataField]
    public LocId? InactiveName;

    [DataField]
    public LocId ActivatePopup = "stories-hunter-arrow-activated";

    [DataField]
    public LocId DeactivatePopup = "stories-hunter-arrow-deactivated";

    [DataField]
    public ProtoId<TagPrototype>? ActiveTag;

    [DataField]
    public ProtoId<TagPrototype>? InactiveTag;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> Skill = "STSkillIllegalTechnology";

    [DataField]
    public TimeSpan? StunTime;

    [DataField]
    public TimeSpan? DazeTime;

    [DataField]
    public float? PushMaxRange;

    [DataField]
    public bool DetachFromNonHumans;
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(HunterArrowSystem))]
public sealed partial class HunterArrowStartActiveComponent : Component;

[Serializable, NetSerializable]
public enum HunterArrowVisuals : byte
{
    Active,
}
