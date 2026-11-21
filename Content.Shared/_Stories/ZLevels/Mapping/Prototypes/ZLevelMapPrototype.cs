using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.ZLevels.Mapping.Prototypes;

[Prototype("zMap")]
public sealed partial class ZLevelMapPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ResPath> Maps = new();

    /// <summary>
    /// Shared components for all zLevels maps
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();
}
