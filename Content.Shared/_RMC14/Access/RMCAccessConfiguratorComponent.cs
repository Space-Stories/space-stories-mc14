using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Access;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RMCAccessConfiguratorSystem))]
public sealed partial class RMCAccessConfiguratorComponent : Component;
