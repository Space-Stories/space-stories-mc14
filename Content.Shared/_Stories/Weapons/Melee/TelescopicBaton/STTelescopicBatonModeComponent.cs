using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Weapons.Melee.TelescopicBaton;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STTelescopicBatonModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool StunMode;

    [DataField(required: true)]
    public DamageSpecifier CombatDamage = new();

    [DataField(required: true)]
    public double StunStaminaDamage;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> StunSkill = "RMCSkillPolice";

    [DataField]
    public Dictionary<int, double> StunSkillMultipliers = new()
    {
        [1] = 1.5,
        [2] = 3,
    };

    [DataField(required: true)]
    public EntityWhitelist StunWhitelist = new();

    [DataField]
    public Dictionary<int, TimeSpan> XenoTierStuns = new()
    {
        [0] = TimeSpan.FromSeconds(3),
        [1] = TimeSpan.FromSeconds(3),
        [2] = TimeSpan.FromSeconds(1.5),
    };

    [DataField]
    public TimeSpan XenoStunCooldown = TimeSpan.FromSeconds(4.5);
}
