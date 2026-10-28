using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged;

[RegisterComponent, NetworkedComponent]
public sealed partial class WallPenetratorComponent : Component
{
    [DataField("penetrationPower")]
    public int PenetrationPower = 6;

    [DataField("damageMultiplier")]
    public float DamageMultiplier = 0.9f;
}