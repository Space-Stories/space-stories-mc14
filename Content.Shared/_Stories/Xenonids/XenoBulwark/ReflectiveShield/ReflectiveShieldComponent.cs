using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.ReflectiveShield;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReflectiveShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public Angle ReflectAngle = Angle.FromDegrees(50);

    [DataField, AutoNetworkedField]
    public float ReflectChance = 1f;

    [DataField, AutoNetworkedField]
    public float ReflectionMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(4.5);

    [DataField, AutoNetworkedField]
    public TimeSpan MinCooldown = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public TimeSpan FullCooldown = TimeSpan.FromSeconds(24);

    [DataField, AutoNetworkedField]
    public float CooldownPerSecond = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan? DeactivateAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? ActivatedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? PendingCooldown;

    [DataField, AutoNetworkedField]
    public Angle FrontalAngle = Angle.FromDegrees(90);

    [DataField, AutoNetworkedField]
    public int PlasmaCost = 100;

    [DataField, AutoNetworkedField]
    public TimeSpan ToggleBuffer = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public List<EntProtoId> PenetratingProjectiles = new()
    {
        "CMBulletSniper10x28mm",
        "RMCProjectileRocket84mm",
        "RMCProjectileRocket84mmAntiArmor",
        "RMCProjectileRocket84mmWhitePhosphorus",
        "RMCBaseBulletSentryFireProjectile",
        "RMCBulletSentryFireProjectile",
        "RMCBulletSentryFireProjectileBlue",
        "RMCBulletSentryFireProjectileSmoke"
    };
}
