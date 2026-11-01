using Robust.Shared.GameStates;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Stories.ZLevels.Combat;

/// <summary>
/// Tracks a projectile transitioning to another Z-level map at the midpoint (50%) of its trajectory.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelProjectileTrajectoryComponent : Component
{
    [DataField, AutoNetworkedField] public Vector2 StartPos;
    [DataField, AutoNetworkedField] public Vector2 TargetPos;
    [DataField, AutoNetworkedField] public float TotalDistance;
    [DataField, AutoNetworkedField] public EntityUid TargetMapUid;
    [DataField, AutoNetworkedField] public int ZOffset;
    [DataField, AutoNetworkedField] public bool HasTransferred;
}
