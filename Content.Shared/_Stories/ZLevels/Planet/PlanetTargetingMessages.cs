using Robust.Shared.Serialization;

namespace Content.Shared._Stories.ZLevels.Planet;

[Serializable, NetSerializable]
public sealed class SupplyDropComputerZLevelBuiMsg(int zLevel) : BoundUserInterfaceMessage
{
    public readonly int ZLevel = zLevel;
}

[Serializable, NetSerializable]
public sealed class OverwatchSupplyDropZLevelBuiMsg(int zLevel) : BoundUserInterfaceMessage
{
    public readonly int ZLevel = zLevel;
}

[Serializable, NetSerializable]
public sealed class OverwatchOrbitalZLevelBuiMsg(int zLevel) : BoundUserInterfaceMessage
{
    public readonly int ZLevel = zLevel;
}
