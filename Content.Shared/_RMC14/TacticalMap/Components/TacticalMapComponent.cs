using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.TacticalMap;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class TacticalMapLayerData
{
    public Guid LastCanvasUpdate;

    [DataField]
    public Dictionary<int, TacticalMapBlip> Blips = new();

    [DataField]
    public Dictionary<int, TacticalMapBlip> LastUpdateBlips = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastUpdateBlipsAt;

    [DataField]
    public List<TacticalMapLine> Lines = new();

    [DataField]
    public Dictionary<Vector2i, TacticalMapLabelData> Labels = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class TacticalMapComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public string MapId = string.Empty;

    [DataField, AutoNetworkedField]
    public string DisplayName = string.Empty;

    [DataField]
    public Dictionary<EntProtoId<TacticalMapLayerComponent>, TacticalMapLayerData> Layers = new();

    [DataField]
    public bool MapDirty;

    [DataField]
    public int MaxCanvasLabels = 128;

    [DataField]
    public int MaxLabelLength = 128;

    [DataField]
    public float MaxLineThickness = 8f;

    [DataField]
    public int ReplayMaxFrames = 2000;

    [DataField]
    public TimeSpan ReplayKeyframeInterval = TimeSpan.FromSeconds(10);
}
