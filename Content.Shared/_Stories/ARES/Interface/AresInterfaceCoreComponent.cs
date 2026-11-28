using Content.Shared._RMC14.ARES.Logs;
using Content.Shared.Access;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.ARES.Interface;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedAresInterfaceSystem))]
public sealed partial class AresInterfaceCoreComponent : Component
{
    [DataField] public int MaxRecords = 256;
    [DataField] public int MaxConversations = 128;
    [DataField] public int MaxLines = 128;
    [DataField] public int MaxTextLength = 512;
    [DataField] public int MaxPrintCharacters = 12000;
    [DataField] public TimeSpan MessageCooldown = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan PrinterCooldown = TimeSpan.FromSeconds(20);
    [DataField] public TimeSpan QuartersCooldown = TimeSpan.FromMinutes(10);
    [DataField] public EntProtoId PaperPrototype = "Paper";
    [DataField] public SoundSpecifier NotificationSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
    [DataField] public SoundSpecifier QuartersSound = new SoundPathSpecifier("/Audio/Misc/redalert.ogg");
    [DataField] public ProtoId<AccessLevelPrototype> TechnicianAccess = "STAccessHermesTechnician";
    [DataField] public ProtoId<AccessLevelPrototype> CorporateCommandAccess = "STAccessAresCorporateCommand";
    [DataField] public ProtoId<AccessLevelPrototype> HighCommandAccess = "CMAccessHighCommand";
    [DataField] public ProtoId<AccessLevelPrototype> CommandingOfficerAccess = "CMAccessCO";
    [DataField] public ProtoId<AccessLevelPrototype> SyntheticAccess = "RMCAccessSynth";
    [DataField] public ProtoId<AccessLevelPrototype> ChiefEngineerAccess = "CMAccessCE";
    [DataField] public ProtoId<AccessLevelPrototype> SeniorCommandAccess = "RMCAccessSeniorCommand";
    [DataField] public ProtoId<AccessLevelPrototype> CorporateAccess = "CMAccessCorp";
    [DataField] public ProtoId<AccessLevelPrototype> CommandAccess = "RMCAccessCommand";
    [DataField] public Dictionary<ProtoId<JobPrototype>, AresAccess> AssignedAccess = new()
    {
        ["CMChiefEngineer"] = AresAccess.ChiefEngineer,
        ["RMCJobSynthetic"] = AresAccess.Synthetic,
    };
    [DataField] public Dictionary<AresPage, EntProtoId<ARESLogTypeComponent>> LogTypes = new()
    {
        [AresPage.Announcements] = "ARESTabAnnouncementLogs",
        [AresPage.Flights] = "ARESTabDropshipLogs",
        [AresPage.Bioscans] = "STARESTabBioscanLogs",
        [AresPage.Bombardments] = "ARESTabOrbitalCannonLogs",
        [AresPage.Security] = "STARESTabSecurityLogs",
        [AresPage.Requisitions] = "ARESTabRequisitionsLogs",
        [AresPage.Technology] = "ARESTabIntelLogs",
        [AresPage.Access] = "STARESTabAccessLogs",
    };

    public int NextConversationId = 1;
    public readonly List<AresConversation> Conversations = [];
    public readonly List<AresTalkLine> DeletedRecords = [];
    public readonly HashSet<EntityUid> Terminals = [];
    public readonly Dictionary<EntityUid, TimeSpan> NextMessages = [];
    [AutoPausedField] public TimeSpan PrinterReadyAt;
    [AutoPausedField] public TimeSpan QuartersReadyAt;
}

public sealed class AresConversation(int id, EntityUid card, string user)
{
    public readonly int Id = id;
    public readonly EntityUid Card = card;
    public readonly string User = user;
    public readonly List<AresTalkLine> Lines = [];
    public bool Archived;
}
