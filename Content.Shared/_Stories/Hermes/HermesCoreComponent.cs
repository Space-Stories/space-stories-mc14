using Content.Shared.Access;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Hermes;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedHermesSystem))]
public sealed partial class HermesCoreComponent : Component
{
    [DataField] public int MaxTickets = 256;
    [DataField] public int MaxLogs = 256;
    [DataField] public int MaxDetailLength = 512;
    [DataField] public TimeSpan RequestCooldown = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan LockdownCooldown = TimeSpan.FromMinutes(2);
    [DataField] public TimeSpan StatusUpdateInterval = TimeSpan.FromSeconds(1);
    [DataField] public ProtoId<AccessLevelPrototype> TemporaryAccess = "STAccessHermesTemporary";
    [DataField] public ProtoId<AccessLevelPrototype> TechnicianAccess = "STAccessHermesTechnician";
    [DataField] public HashSet<ProtoId<AccessLevelPrototype>> CertifiedAccess = ["CMAccessCO", "RMCAccessSynth", "STAccessHermes"];
    [DataField] public ProtoId<AccessLevelPrototype> ChiefEngineerAccess = "CMAccessCE";
    [DataField] public ProtoId<AccessLevelPrototype> SeniorCommandAccess = "RMCAccessSeniorCommand";
    [DataField] public ProtoId<JobPrototype> ChiefEngineerJob = "CMChiefEngineer";
    [DataField] public HashSet<ProtoId<AccessLevelPrototype>> ReporterAccess = ["CMAccessEngineering", "RMCAccessSeniorCommand", "CMAccessCorp"];
    [DataField] public HashSet<HermesCategory> PriorityCategories = [HermesCategory.MajorDamage, HermesCategory.Fire, HermesCategory.Communications, HermesCategory.PowerGeneration];
    [DataField] public Dictionary<HermesIff, HashSet<string>> FriendlyFactions = new()
    {
        [HermesIff.Marines] = ["UNMC", "ARES"],
        [HermesIff.Corporate] = ["WeYa", "ARES"],
        [HermesIff.Combined] = ["UNMC", "WeYa", "ARES"],
        [HermesIff.Ares] = ["ARES"],
    };
    [DataField] public SoundSpecifier NotificationSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
    [DataField] public SoundSpecifier LockdownSound = new SoundPathSpecifier("/Audio/Misc/redalert.ogg");
    [DataField] public bool RequireProcessor = true;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LockdownReadyAt;

    public int NextTicketId = 1;
    public int NextVentId = 1;
    public bool LockedDown;
    public HermesIff Iff;
    public readonly List<HermesTicket> Tickets = [];
    public readonly List<HermesLogEntry> Logs = [];
    public readonly List<HermesLogEntry> LoginLogs = [];
    public readonly HashSet<EntityUid> Terminals = [];
    public readonly Dictionary<EntityUid, TimeSpan> NextRequests = [];
    public readonly Dictionary<string, TimeSpan> SensorCooldowns = [];
    public bool? LastOnline;
}

public sealed class HermesTicket
{
    public int Id;
    public bool AccessRequest;
    public EntityUid Card;
    public string Submitter = string.Empty;
    public string Details = string.Empty;
    public HermesCategory Category;
    public bool Priority;
    public HermesTicketStatus Status;
    public EntityUid? AssigneeCard;
    public string? Assignee;
    public TimeSpan CreatedAt;
}
