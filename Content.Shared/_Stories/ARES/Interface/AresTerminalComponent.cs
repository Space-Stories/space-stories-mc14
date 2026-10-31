using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.ARES.Interface;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedAresInterfaceSystem))]
public sealed partial class AresTerminalComponent : Component
{
    public override bool SessionSpecific => true;

    [DataField] public EntProtoId<IFFFactionComponent> Faction = "FactionMarine";
    [DataField, AutoNetworkedField] public int MaxTextLength = 512;
    [AutoNetworkedField] public bool Connected;
    [AutoNetworkedField] public AresAccess Access;
    [AutoNetworkedField] public string User = string.Empty;
    [AutoNetworkedField] public bool Sudo;
    [AutoNetworkedField] public AresPage Page = AresPage.Talk;
    [AutoNetworkedField] public List<AresPage> Pages = [];
    [AutoNetworkedField] public List<AresRecordView> Records = [];
    [AutoNetworkedField] public List<AresTalkLine> Conversation = [];
    [AutoNetworkedField] public int? ConversationId;
    [AutoNetworkedField] public AresSecurityView? Security;
    [AutoNetworkedField] public bool Evacuating;
    [AutoNetworkedField] public string Feedback = string.Empty;
    [AutoNetworkedField] public List<EntProtoId<ARESLogTypeComponent>> AvailableLogs = [];
    [AutoNetworkedField] public EntProtoId<ARESLogTypeComponent>? SelectedLog;
    [AutoNetworkedField] public List<string> LogEntries = [];
    [AutoNetworkedField] public int LogIndex;
    [AutoNetworkedField] public int LogCount;

    public EntityUid? Core;
    // One operator per physical console keeps private transcripts and restricted logs out of other sessions.
    public EntityUid? Operator;
    public EntityUid? Card;
    public GameTick LogAccessRevision;
    public string? SudoName;
    public readonly Dictionary<int, AresLogReference> LogReferences = [];
}

public sealed record AresLogReference(EntProtoId<ARESLogTypeComponent> Type, int Index, string Text);
