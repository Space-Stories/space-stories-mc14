using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Hunter.Marking.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
[Access(typeof(SharedHunterMarkingSystem), typeof(Content.Shared._Stories.Xenonids.Predalien.STPredalienSystem))]
public sealed partial class HunterMarkedComponent : Component
{
    [DataField] [AutoNetworkedField]
    public SpriteSpecifier.Rsi BloodedIcon = new(
        new ResPath("/Textures/_Stories/Interface/Misc/hunter_marks.rsi"),
        "blooded"
    );

    [DataField] [AutoNetworkedField]
    public string? BloodedReason;

    [DataField] [AutoNetworkedField]
    public SpriteSpecifier.Rsi DishonoredIcon = new(
        new ResPath("/Textures/_Stories/Interface/Misc/hunter_marks.rsi"),
        "dishonored"
    );

    [DataField] [AutoNetworkedField]
    public string? DishonoredReason;

    [DataField] [AutoNetworkedField]
    public SpriteSpecifier.Rsi GearCarrierIcon = new(
        new ResPath("/Textures/_Stories/Interface/Misc/hunter_marks.rsi"),
        "gear"
    );

    [DataField] [AutoNetworkedField]
    public SpriteSpecifier.Rsi HonoredIcon = new(
        new ResPath("/Textures/_Stories/Interface/Misc/hunter_marks.rsi"),
        "honored"
    );

    [DataField] [AutoNetworkedField]
    public string? HonoredReason;

    [DataField] [AutoNetworkedField]
    public NetEntity? Hunter;

    [DataField] [AutoNetworkedField]
    public HunterMarkType Marks = HunterMarkType.None;

    [DataField] [AutoNetworkedField]
    public SpriteSpecifier.Rsi PreyIcon = new(
        new ResPath("/Textures/_Stories/Interface/Misc/hunter_marks.rsi"),
        "hunted"
    );

    [DataField] [AutoNetworkedField]
    public SpriteSpecifier.Rsi ThralledIcon = new(
        new ResPath("/Textures/_Stories/Interface/Misc/hunter_marks.rsi"),
        "thralled"
    );

    [DataField] [AutoNetworkedField]
    public string? ThralledReason;
}

[Flags]
[Serializable] [NetSerializable]
public enum HunterMarkType : byte
{
    None = 0,
    Prey = 1 << 0,
    Honored = 1 << 1,
    Dishonored = 1 << 2,
    GearCarrier = 1 << 3,
    Thralled = 1 << 4,
    Blooded = 1 << 5,
}
