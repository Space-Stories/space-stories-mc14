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

    [DataField, AutoNetworkedField]
    public EntityUid? Caster;
}
