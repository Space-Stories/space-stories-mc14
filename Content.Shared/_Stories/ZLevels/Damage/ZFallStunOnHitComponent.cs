using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Stories.ZLevels.Damage;

/// <summary>
/// Attached to falling objects/mobs that are allowed to stun targets when falling onto them.
/// By default, falling items DO NOT stun targets unless this component is present.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZFallStunOnHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public float StunMultiplier = 1.0f;

    [DataField, AutoNetworkedField]
    public float MinSpeed = 1.0f;
}
