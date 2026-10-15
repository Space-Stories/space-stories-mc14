// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Dropship.Weapon;

public sealed partial class DropshipWeaponComponent
{
    /// <summary>
    /// Strength of the single camera kick applied to passengers when firing. Zero disables it.
    /// </summary>
    [DataField]
    public int STLaunchCameraShakeStrength;

    /// <summary>
    /// Chance for each unbuckled passenger to fall when a weapon with camera recoil fires.
    /// </summary>
    [DataField]
    public float STLaunchKnockdownChance = 0.70f;

    /// <summary>
    /// Base duration of the launch knockdown, using the same status modifiers as takeoff and landing.
    /// </summary>
    [DataField]
    public TimeSpan STLaunchKnockdownDuration = TimeSpan.FromSeconds(1.5);
}
