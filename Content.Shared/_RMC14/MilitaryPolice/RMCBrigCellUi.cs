using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.MilitaryPolice;

[Serializable, NetSerializable]
public enum RMCBrigCellUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed record RMCBrigReportView(NetEntity Paper, RMCIncidentReport Report);

[Serializable, NetSerializable]
public sealed class RMCBrigCellUiData(
    string cellName,
    List<RMCBrigReportView> reports,
    NetEntity? viewedReport,
    NetEntity? activeReport,
    TimeSpan? finishAt,
    uint revision,
    bool doorsOpen,
    bool canToggleDoors,
    TimeSpan startedAt = default)
{
    public readonly string CellName = cellName;
    public readonly List<RMCBrigReportView> Reports = reports;
    public readonly NetEntity? ViewedReport = viewedReport;
    public readonly NetEntity? ActiveReport = activeReport;
    public readonly TimeSpan? FinishAt = finishAt;
    public readonly TimeSpan StartedAt = startedAt;
    public readonly uint Revision = revision;
    public readonly bool DoorsOpen = doorsOpen;
    public readonly bool CanToggleDoors = canToggleDoors;
}

[Serializable, NetSerializable]
public enum RMCBrigCellAction : byte
{
    Select,
    Start,
    Pause,
    End,
    Reset,
    Pardon,
    Eject,
    ToggleDoors,
}

[Serializable, NetSerializable]
public sealed class RMCBrigCellMsg(RMCBrigCellAction action, uint revision, NetEntity? paper = null)
    : BoundUserInterfaceMessage
{
    public readonly RMCBrigCellAction Action = action;
    public readonly uint Revision = revision;
    public readonly NetEntity? Paper = paper;
}
