using Content.Shared.Damage;

namespace Content.Shared._Stories.ZLevels.Damage.FallingDamage;

public sealed partial class FallingDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FallingDamageComponent, ZFellOnMeEvent>(OnFallOnMe);
    }

    private void OnFallOnMe(Entity<FallingDamageComponent> ent, ref ZFellOnMeEvent args)
    {
        _damageable.TryChangeDamage(args.Fallen, ent.Comp.Damage * args.Speed);
    }
}
