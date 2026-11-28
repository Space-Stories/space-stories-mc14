using Content.Server._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Mobs.Components;

namespace Content.Server._Stories.FunFacts;

public sealed class FunFactsSurgerySnapshotSystem : EntitySystem
{
    [Dependency] private readonly FunFactsSystem _funFacts = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateComponent, CMSurgeryDoAfterEvent>(OnSurgeryDoAfter, before: [typeof(CMSurgerySystem)]);
    }

    private void OnSurgeryDoAfter(Entity<MobStateComponent> ent, ref CMSurgeryDoAfterEvent args)
    {
        _funFacts.MarkSurgeryStepPending(ent.Owner, args);
    }
}
