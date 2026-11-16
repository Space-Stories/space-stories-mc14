using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Traps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HunterTrapSystem))]
public sealed partial class HunterHealBlockedComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Sources = new();
}
