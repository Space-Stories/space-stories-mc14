using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Despoiler;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoDespoilerAcidBarrageProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Shooter;

    [DataField, AutoNetworkedField]
    public Vector2 Scale = Vector2.One;
}
