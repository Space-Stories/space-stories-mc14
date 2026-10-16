using Content.Shared._RMC14.Armor;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerHypertensionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly CMArmorSystem _armor = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoDespoilerHypertensionComponent, CMGetArmorEvent>(OnGetArmor);
    }

    private void OnGetArmor(Entity<XenoDespoilerHypertensionComponent> ent, ref CMGetArmorEvent args)
    {
        args.XenoArmor += ent.Comp.Stacks / 2 * ent.Comp.ArmorPerStackPair;
    }

    public void AddSlashPoints(EntityUid uid, XenoDespoilerHypertensionComponent comp)
    {
        GainPoints(uid, comp, comp.PointsPerSlash);
    }

    public void GainPoints(EntityUid uid, XenoDespoilerHypertensionComponent comp, float amount)
    {
        if (amount <= 0)
            return;

        comp.Points += amount;
        comp.LastActivityAt = _timing.CurTime;

        var before = comp.Stacks;
        while (comp.Points >= comp.PointsPerStack && comp.Stacks < comp.MaxStacks)
        {
            comp.Points -= comp.PointsPerStack;
            comp.Stacks++;
        }

        if (comp.Stacks >= comp.MaxStacks)
            comp.Points = 0;

        FinishMutation(uid, comp, comp.Stacks != before);
    }

    public bool TryConsumeStacks(EntityUid uid, XenoDespoilerHypertensionComponent comp, int count)
    {
        if (comp.Stacks < count)
            return false;

        comp.Stacks -= count;
        FinishMutation(uid, comp, true);
        return true;
    }

    public void ReduceStacks(EntityUid uid, XenoDespoilerHypertensionComponent comp, int amount)
    {
        var lost = Math.Clamp(amount, 0, comp.Stacks);
        if (lost == 0)
            return;

        comp.Stacks -= lost;
        FinishMutation(uid, comp, true);
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

            var before = comp.Stacks;
            comp.Points -= comp.DecayPerSecond * frameTime;
            while (comp.Points < 0 && comp.Stacks > 0)
            {
                comp.Stacks--;
                comp.Points += comp.PointsPerStack;
            }

            if (comp.Stacks <= 0 && comp.Points < 0)
                comp.Points = 0;

            if (comp.Stacks != before)
                FinishMutation(uid, comp, true);
        }
    }

    private void FinishMutation(EntityUid uid, XenoDespoilerHypertensionComponent comp, bool stacksChanged)
    {
        if (stacksChanged && TryComp<CMArmorComponent>(uid, out var armor))
            _armor.UpdateArmorValue((uid, armor));

        Dirty(uid, comp);
    }
}
