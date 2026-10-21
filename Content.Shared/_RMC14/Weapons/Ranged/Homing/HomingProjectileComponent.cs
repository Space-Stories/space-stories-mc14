using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged.Homing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState()]
public sealed partial class HomingProjectileComponent : Component
{
    /// <summary>
    ///     The target of the homing projectile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity Target = NetEntity.Invalid; // Stories-DeletedEntityReferences

    /// <summary>
    ///     The speed of the homing projectile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ProjectileSpeed = 62;
}
