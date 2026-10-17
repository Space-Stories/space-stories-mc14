using Content.Shared.Damage;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Juggernaut;

public sealed class JuggernautBarrelHeatSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<JuggernautBarrelHeatComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<JuggernautBarrelHeatComponent, GunShotEvent>(OnShot);
    }

    private void OnAttemptShoot(Entity<JuggernautBarrelHeatComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp(ent, out DamageableComponent? damageable) &&
            damageable.TotalDamage >= ent.Comp.DisableAtDamage)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("st-juggernaut-barrel-too-damaged");
        }
    }

    private void OnShot(Entity<JuggernautBarrelHeatComponent> ent, ref GunShotEvent args)
    {
        // Server-only: client prediction reruns would double-count the tick counters.
        if (_net.IsClient)
            return;

        var comp = ent.Comp;
        var now = _timing.CurTime;

        if (comp.FiringSince is not { } firingSince)
        {
            comp.FiringSince = now;
            comp.NextDamageTickAt = now + comp.DamageStartAfter;
            comp.TicksDealt = 0;
        }
        else
        {
            var gap = now - comp.LastShotAt;
            if (gap > comp.SustainedFireGrace)
            {
                // Pause costs 1:1 against banked fire time instead of wiping it outright - stops pulsing
                // fire just under SustainedFireGrace from resetting the clock for free.
                if (gap >= comp.LastShotAt - firingSince)
                {
                    comp.FiringSince = now;
                    comp.NextDamageTickAt = now + comp.DamageStartAfter;
                    comp.TicksDealt = 0;
                }
                else
                {
                    comp.FiringSince = firingSince + gap;
                    comp.NextDamageTickAt += gap;
                }
            }
        }

        comp.LastShotAt = now;

        while (comp.NextDamageTickAt is { } next && now >= next)
        {
            comp.TicksDealt++;
            var damage = comp.BaseDamagePerTick + comp.DamagePerTickIncrease * (comp.TicksDealt - 1);
            var spec = new DamageSpecifier();
            spec.DamageDict["Heat"] = damage;
            _damageable.TryChangeDamage(ent, spec, ignoreResistances: true);

            comp.NextDamageTickAt = next + TimeSpan.FromSeconds(1);
        }
    }
}
