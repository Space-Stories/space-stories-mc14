using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Predalien;

[RegisterComponent, NetworkedComponent]
[Access(typeof(STPredalienSystem))]
public sealed partial class STPredalienLarvaComponent : Component;
