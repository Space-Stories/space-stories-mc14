using Robust.Shared.GameStates;

namespace Content.Shared._Stories.ZLevels.Damage.SafeFalling;

/// <summary>
/// Reduces damage from falling on this entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SafeFallingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 0f;

    [DataField, AutoNetworkedField]
    public float StunMultiplier = 0f;
}
