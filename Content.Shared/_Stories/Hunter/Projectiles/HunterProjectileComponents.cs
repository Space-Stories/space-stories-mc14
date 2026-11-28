using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Projectiles;

[RegisterComponent, NetworkedComponent]
public sealed partial class HunterStunBoltComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(4);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class HunterAreaStunOnHitComponent : Component
{
    public bool Detonated = false;

    [DataField]
    public TimeSpan HunterReductionTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float Radius = 7f;

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(6);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class RifleBoltComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(4);

    [DataField]
    public DamageSpecifier BonusXenoDamage = new();
}
