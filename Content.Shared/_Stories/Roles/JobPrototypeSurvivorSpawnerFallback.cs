using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Roles;
// ReSharper restore CheckNamespace

public sealed partial class JobPrototype
{
    [DataField]
    public List<ProtoId<JobPrototype>> SurvivorSpawnerFallback { get; private set; } = new();
}
