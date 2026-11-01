using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hermes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedHermesSystem))]
public sealed partial class HermesTerminalComponent : Component
{
    public override bool SessionSpecific => true;

    [DataField] public EntProtoId<IFFFactionComponent> Faction = "FactionMarine";
    [DataField] public bool Portable;
    [DataField, AutoNetworkedField] public bool NotificationsEnabled = true;
    [AutoNetworkedField] public bool Connected;
    [AutoNetworkedField] public int MaxDetailLength = 512;
    [AutoNetworkedField] public List<HermesTicketView> Tickets = [];

    public EntityUid? Core;
    public readonly Dictionary<EntityUid, HermesSession> Sessions = [];
}

public sealed class HermesSession(EntityUid card)
{
    public readonly EntityUid Card = card;
    public HermesAccess Access;
}
