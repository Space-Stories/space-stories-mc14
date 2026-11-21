using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerHypertensionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public void AddSlashPoints(EntityUid uid, XenoDespoilerHypertensionComponent comp)
    {
        AddPoints(uid, comp, comp.PointsPerSlash);
    }

    public void AddPoints(EntityUid uid, XenoDespoilerHypertensionComponent comp, float amount)
    {
        if (amount <= 0)
            return;

        comp.Points += amount;
        comp.LastActivityAt = _timing.CurTime;

        while (comp.Points >= comp.PointsPerStack && comp.Stacks < comp.MaxStacks)
        {
            comp.Points -= comp.PointsPerStack;
            comp.Stacks++;
        }

        if (comp.Stacks >= comp.MaxStacks)
            comp.Points = 0;

        Dirty(uid, comp);
    }

    public bool TrySpendStacks(EntityUid uid, XenoDespoilerHypertensionComponent comp, int count)
    {
        if (comp.Stacks < count)
            return false;

        comp.Stacks -= count;
        Dirty(uid, comp);
        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoDespoilerHypertensionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Stacks <= 0 && comp.Points <= 0)
                continue;

            if (now - comp.LastActivityAt < comp.DecayDelay)
                continue;

            var stacks = comp.Stacks;
            comp.Points -= comp.DecayPerSecond * frameTime;
            while (comp.Points < 0 && comp.Stacks > 0)
            {
                comp.Stacks--;
                comp.Points += comp.PointsPerStack;
            }

            if (comp.Stacks <= 0 && comp.Points < 0)
                comp.Points = 0;

            if (comp.Stacks != stacks)
                Dirty(uid, comp);
        }
    }
}
