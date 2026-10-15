using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

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

    [DataField(required: true)]
    public TimeSpan StunDuration;

    [DataField(required: true)]
    public EntityWhitelist StunWhitelist = new();
}
