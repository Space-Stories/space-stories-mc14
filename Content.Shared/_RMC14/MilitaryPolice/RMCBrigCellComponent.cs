using Content.Shared.Access;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.MilitaryPolice;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCBrigCellComponent : Component
{
    public const string ReportContainer = "brig_reports";

    public const string StartPort = "Start";
    public const string OpenPort = "RMCBrigOpen";
    public const string ClosePort = "RMCBrigClose";

    [DataField]
    public int MaxReports = 8;

    [DataField]
    public int CellNumber;

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> PardonAccess = ["CMAccessCO", "CMAccessHighCommand"];

    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "MarineMilitaryPolice";

    [DataField]
    public EntityUid? ViewedReport;

    [DataField]
    public EntityUid? ActiveReport;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan StartedAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? FinishAt;

    [DataField]
    public uint Revision;

    public bool DoorsOpen;
    public bool CanToggleDoors;
}
