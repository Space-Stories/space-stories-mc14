namespace Content.Shared._Stories.ZLevels.Damage.SafeFalling;

public sealed class SafeFallingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SafeFallingComponent, ZFallingDamageCalculateEvent>(OnFallingDamageCalculate);
    }

    private void OnFallingDamageCalculate(Entity<SafeFallingComponent> ent, ref ZFallingDamageCalculateEvent args)
    {
        if (args.Fallen == ent.Owner)
            return;

        args.DamageMultiplier *= ent.Comp.DamageMultiplier;
        args.StunMultiplier *= ent.Comp.StunMultiplier;
    }
}
