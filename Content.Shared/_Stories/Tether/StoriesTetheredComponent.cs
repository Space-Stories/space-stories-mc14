using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Tether;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StoriesTetherSystem))]
public sealed partial class StoriesTetheredComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Origin;

    [DataField, AutoNetworkedField]
    public int Range = 5;

    [DataField, AutoNetworkedField]
    public float BreakDistance = 3f;

    [DataField, AutoNetworkedField]
    public bool Resistible = true;

    [DataField]
    public TimeSpan ResistTime = TimeSpan.FromSeconds(15);

    [DataField]
    public ProtoId<AlertPrototype> Alert = "STTethered";
}
