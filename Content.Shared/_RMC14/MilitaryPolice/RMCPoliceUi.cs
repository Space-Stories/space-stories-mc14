using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.MilitaryPolice;

[Serializable, NetSerializable]
public enum RMCPoliceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RMCPoliceUiData(
    bool connected,
    List<RMCPoliceRecordView> records,
    RMCIncidentReport? report,
    uint revision,
    bool scannerConnected,
    List<RMCIncidentEntry> fingerprints,
    EntProtoId<RMCMarineLawCatalogueComponent> lawCatalogue)
{
    public readonly bool Connected = connected;
    public readonly List<RMCPoliceRecordView> Records = records;
    public readonly RMCIncidentReport? Report = report;
    public readonly uint Revision = revision;
    public readonly bool ScannerConnected = scannerConnected;
    public readonly List<RMCIncidentEntry> Fingerprints = fingerprints;
    public readonly EntProtoId<RMCMarineLawCatalogueComponent> LawCatalogue = lawCatalogue;
}

[Serializable, NetSerializable]
public sealed record RMCPoliceRecordView(uint Id, GeneralStationRecord General, RMCPoliceRecord? Security)
{
    // The station record key stays unchanged for IDs, HUDs and other consoles.
    public ulong DisplayId => (ulong) Id + 1;
}

[Serializable, NetSerializable]
public enum RMCPoliceRecordAction : byte
{
    CreateGeneral,
    CreateSecurity,
    DeleteGeneral,
    EditGeneral,
    Status,
    PhysicalStatus,
    MentalStatus,
    Comment,
    DeleteComment,
    Print,
    PrintFingerprints,
    ClearFingerprints,
    EjectScanner,
    LinkPerson,
}

[Serializable, NetSerializable]
public sealed class RMCPoliceRecordSelectedMsg(uint id) : BoundUserInterfaceMessage
{
    public readonly uint Id = id;
}

[Serializable, NetSerializable]
public sealed class RMCPoliceRecordMsg(
    RMCPoliceRecordAction action,
    uint id = 0,
    string text = "",
    string field = "",
    int index = 0) : BoundUserInterfaceMessage
{
    public readonly RMCPoliceRecordAction Action = action;
    public readonly uint Id = id;
    public readonly string Text = text;
    public readonly string Field = field;
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public enum RMCSentencingAction : byte
{
    New,
    Scrap,
    Suspect,
    Summary,
    AddCharge,
    RemoveCharge,
    RelatedCrime,
    AddWitness,
    EditWitness,
    RemoveWitness,
    AddEvidence,
    EditEvidence,
    RemoveEvidence,
    Export,
}

[Serializable, NetSerializable]
public sealed class RMCSentencingMsg(
    RMCSentencingAction action,
    uint revision,
    string text = "",
    int index = 0) : BoundUserInterfaceMessage
{
    public readonly RMCSentencingAction Action = action;
    public readonly uint Revision = revision;
    public readonly string Text = text;
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class RMCOverwatchInsubordinationMsg(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}
