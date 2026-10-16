using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Despoiler;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoDespoilerLingeringAcidComponent : Component
{
    [DataField]
    public TimeSpan MinLifetime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MaxLifetime = TimeSpan.FromSeconds(20);

    [DataField]
    public float CrossBurnDamage = 20f;

    [DataField]
    public TimeSpan CrossSlow = TimeSpan.FromSeconds(0.4);

    [DataField]
    public int SpraysToExtinguish = 2;

    public int SpraysTaken;

    public TimeSpan LastSprayAt;

    [DataField]
    public float BarricadeDamagePerSecond = 5f;

    public TimeSpan NextBarricadeDamageAt;

    [DataField, AutoNetworkedField]
    public EntityUid? Caster;
}
