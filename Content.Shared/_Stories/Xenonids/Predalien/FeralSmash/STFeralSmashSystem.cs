using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralSmash;

public sealed class STFeralSmashSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STFeralSmashComponent, XenoLeapHitEvent>(OnFeralSmashLeapHit);
    }

    private void OnFeralSmashLeapHit(Entity<STFeralSmashComponent> xeno, ref XenoLeapHitEvent args)
    {
        RemComp<XenoLeapingComponent>(xeno.Owner);

        if (TryComp<MeleeWeaponComponent>(xeno.Owner, out var melee))
        {
            melee.NextAttack = _timing.CurTime;
            Dirty(xeno.Owner, melee);
        }

        _pulling.TryStartPull(xeno.Owner, args.Hit);

        var kills = TryComp<STPredalienComponent>(xeno.Owner, out var predalien)
            ? Math.Min(predalien.Kills, predalien.MaxKills)
            : 0;

        if (kills <= 0 || !_proto.TryIndex<DamageGroupPrototype>("Brute", out var brute))
            return;

        var bonus = new DamageSpecifier(brute, xeno.Comp.DamagePerKill * kills);
        _damageable.TryChangeDamage(args.Hit, bonus, origin: xeno.Owner, tool: xeno.Owner);
    }
}
