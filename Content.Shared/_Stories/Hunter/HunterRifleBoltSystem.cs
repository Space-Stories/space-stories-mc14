using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hunter.Projectiles;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Robust.Shared.Network;

namespace Content.Shared._Stories.Hunter;

public sealed class RifleBoltSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RifleBoltComponent, ProjectileHitEvent>(OnRifleBoltHit);
    }

    private void OnRifleBoltHit(Entity<RifleBoltComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;

        if (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
            return;

        if (_net.IsClient)
            return;

        var shooter = ent.Owner;

        if (HasComp<MarineComponent>(target))
        {
            _stun.TryParalyze(target, ent.Comp.StunTime, true);
        }
        else if (HasComp<XenoComponent>(target) && !ent.Comp.BonusXenoDamage.Empty)
        {
            _damageable.TryChangeDamage(target, ent.Comp.BonusXenoDamage, origin: shooter);
        }
    }
}
