using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.Banish;

[RegisterComponent, NetworkedComponent]
[Access(typeof(STXenoBanishSystem))]
public sealed partial class STBanishedHiveComponent : Component
{
    [DataField]
    public HashSet<string> BlockedActionEvents = new();

    [DataField]
    public HashSet<EntProtoId> NoLarvaRefund = new();
}
