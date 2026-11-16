using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Projectiles;

[RegisterComponent, NetworkedComponent]
public sealed partial class HunterStunBoltComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan HumanStunTime = TimeSpan.FromSeconds(8);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class HunterAreaStunOnHitComponent : Component
{
    public bool Detonated = false;

    [DataField]
    public TimeSpan HunterReductionTime = TimeSpan.FromSeconds(4);

    [DataField]
    public float Radius = 7f;

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(12);
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(HunterPlasmaCasterProjectileSystem))]
public sealed partial class HunterProjectileMaxRangeComponent : Component
{
    [DataField(required: true)]
    public float Range;
}

[RegisterComponent]
[Access(typeof(HunterPlasmaCasterProjectileSystem))]
public sealed partial class HunterProjectileFlyingComponent : Component
{
    [ViewVariables]
    public MapCoordinates Origin;

    [ViewVariables]
    public float Range;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class HunterProjectileRangeGunComponent : Component;

[ByRefEvent]
public readonly record struct HunterProjectileMaxRangeEvent;

[RegisterComponent, NetworkedComponent]
public sealed partial class HunterTriggerOnHitComponent : Component;
