using Content.Shared._Stories.ZLevels.Ladder;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Ladder;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedLadderSystem), typeof(SharedZLevelLadderSystem))] // Stories-ZLevels
public sealed partial class LadderWatchingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Watching;
}
