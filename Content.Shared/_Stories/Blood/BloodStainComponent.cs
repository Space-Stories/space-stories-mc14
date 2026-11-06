using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Blood;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(BloodStainSystem))]
public sealed partial class BloodStainComponent : Component
{
    [DataField]
    public bool CanStain = true;

    [DataField]
    [Access(typeof(BloodStainSystem), Other = AccessPermissions.Read)]
    public string? WornState;

    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField]
    public TimeSpan CleanDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> CleaningReagents = new()
    {
        "Water",
        "SpaceCleaner",
        "SoapReagent",
        "Bleach",
    };

    [DataField]
    public bool AddedReactive;
}
