using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.ZLevels.Ladder;

public enum ZLevelLadderDirection : byte
{
    Down = 0,
    Up = 1,
    Both = 2,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ZLevelLadderComponent : Component
{
    [DataField, AutoNetworkedField]
    public ZLevelLadderDirection Direction = ZLevelLadderDirection.Both;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public float Range = SharedInteractionSystem.InteractionRange + 0.1f;

    [DataField, AutoNetworkedField]
    public bool HasUp;

    [DataField, AutoNetworkedField]
    public bool HasDown;

    [DataField, AutoNetworkedField]
    public EntityUid? LastDoAfterEnt;

    [DataField, AutoNetworkedField]
    public ushort? LastDoAfterId;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastDoAfterTime;
}

[Serializable, NetSerializable]
public sealed partial class ZLevelLadderDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public bool Up { get; set; }

    public ZLevelLadderDoAfterEvent(bool up = true)
    {
        Up = up;
    }
}

[Serializable, NetSerializable]
public enum ZLevelLadderUiKey : byte
{
    Key,
}


[Serializable, NetSerializable]
public sealed class ZLevelLadderClimbBuiMessage(bool up) : BoundUserInterfaceMessage
{
    public bool Up { get; } = up;
}

[Serializable, NetSerializable]
public sealed class ZLevelLadderOpenRadialMessage : EntityEventArgs
{
    public NetEntity Ladder { get; }
    public bool HasUp { get; }
    public bool HasDown { get; }

    public ZLevelLadderOpenRadialMessage(NetEntity ladder, bool hasUp, bool hasDown)
    {
        Ladder = ladder;
        HasUp = hasUp;
        HasDown = hasDown;
    }
}

[Serializable, NetSerializable]
public sealed class ZLevelLadderClimbRequestMessage : EntityEventArgs
{
    public NetEntity Ladder { get; }
    public bool Up { get; }

    public ZLevelLadderClimbRequestMessage(NetEntity ladder, bool up)
    {
        Ladder = ladder;
        Up = up;
    }
}
