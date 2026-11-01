using Robust.Server.GameStates;

namespace Content.Server._Stories.ZLevels.Core.PVS;

public sealed class StoriesPvsOverrideSystem : EntitySystem
{
    [Dependency] private readonly Robust.Server.GameStates.PvsOverrideSystem _pvs = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PvsOverrideComponent, ComponentStartup>(OnPvsStartup);
        SubscribeLocalEvent<PvsOverrideComponent, ComponentShutdown>(OnPvsShutdown);
    }

    private void OnPvsShutdown(Entity<PvsOverrideComponent> ent, ref ComponentShutdown args)
    {
        _pvs.RemoveGlobalOverride(ent);
    }

    private void OnPvsStartup(Entity<PvsOverrideComponent> ent, ref ComponentStartup args)
    {
        _pvs.AddGlobalOverride(ent);
    }
}
