using Content.Shared._RMC14.Synth;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth;

public sealed class STJobVariantGenerationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STJobVariantGenerationComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete,
            before: new[] { typeof(SharedSynthGenerationSystem) });
    }

    private void OnPlayerSpawnComplete(Entity<STJobVariantGenerationComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        ent.Comp.SpawnCompleted = true;

        if (args.JobId == null)
            return;

        if (!args.Profile.VariantPreferences.TryGetValue(args.JobId, out var variant) || variant == null)
            return;

        if (!ent.Comp.Variants.TryGetValue(variant, out var generation) || !_prototype.HasIndex(generation))
            return;

        var comp = EnsureComp<SynthGenerationComponent>(ent.Owner);
        comp.Generation = generation;
        Dirty(ent.Owner, comp);
    }
}
