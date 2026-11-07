using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Requisitions;

public sealed class RequisitionPresetsSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resources = default!;

    public static readonly ResPath PresetsPath = new("/requisitions/presets.yml");

    private RequisitionPresetStore? _store;

    public RequisitionPresetStore GetStore()
    {
        return _store ??= new RequisitionPresetStore(_resources.UserData, PresetsPath);
    }
}
