using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.MilitaryPolice;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class RMCPoliceConsoleComponent : Component
{
    public const string ScannerSlot = "PoliceScanner";

    [DataField, AutoNetworkedField]
    public bool Sentencing;

    [DataField]
    public RMCIncidentReport? Report;

    [DataField]
    public uint Revision;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPrint;

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg");

    [DataField]
    public EntProtoId ReportPaper = "RMCIncidentReportPaper";

    [DataField]
    public EntProtoId RecordPaper = "RMCPoliceRecordPaper";
}

[Serializable, NetSerializable]
public enum RMCPoliceConsoleVisuals : byte
{
    Broken,
}

[Serializable, NetSerializable]
public enum RMCPoliceConsoleVisualLayers : byte
{
    Unpowered,
    Broken,
}
