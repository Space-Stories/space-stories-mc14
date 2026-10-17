using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

[RegisterComponent, NetworkedComponent]
[Access(typeof(CMGunSystem))]
[SpecialistSkillComponent("Juggernaut")]
public sealed partial class JuggernautWhitelistComponent : Component;
