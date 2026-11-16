using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Synth.ResetKey;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StoriesSynthResetKeyRestrictionSystem))]
public sealed partial class StoriesSynthResetKeyRestrictionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public LocId Popup = "stories-synth-reset-key-wrong-type";
}
