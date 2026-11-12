using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Client._RMC14.Requisitions;

[DataDefinition]
public sealed partial class RequisitionPresetFile
{
    [DataField(required: true)]
    public int Version = 1;

    [DataField(required: true)]
    public List<RequisitionPreset> Presets = new();
}

[DataDefinition]
public sealed partial class RequisitionPreset
{
    [DataField(required: true)]
    public string Id { get; set; } = string.Empty;

    [DataField(required: true)]
    public string Name { get; set; } = string.Empty;

    [DataField(required: true)]
    public string Reason { get; set; } = string.Empty;

    [DataField(required: true)]
    public List<RequisitionPresetItem> Items { get; set; } = new();
}

[DataDefinition]
public sealed partial class RequisitionPresetItem
{
    [DataField(required: true)]
    public string Key { get; set; } = string.Empty;

    [DataField(required: true)]
    public string Name { get; set; } = string.Empty;

    [DataField(required: true)]
    public int Amount { get; set; }
}
