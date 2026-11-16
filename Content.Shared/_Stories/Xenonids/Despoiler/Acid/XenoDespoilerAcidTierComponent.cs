using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.Despoiler;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoDespoilerAcidSystem))]
public sealed partial class XenoDespoilerAcidTierComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Tier;

    [DataField, AutoNetworkedField]
    public int MaxTier = 4;
}
