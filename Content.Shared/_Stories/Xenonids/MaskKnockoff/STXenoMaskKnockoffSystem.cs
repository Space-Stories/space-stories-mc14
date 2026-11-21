using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Pheromones;
using Content.Shared._Stories.Hunter.Vision;
using Content.Shared._Stories.Xenonids.Predalien;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Xenonids.MaskKnockoff;

public sealed class STXenoMaskKnockoffSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STXenoMaskKnockoffComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<STXenoMaskKnockoffComponent> xeno, ref MeleeHitEvent args)
    {
        if (_net.IsClient)
            return;

        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var target in args.HitEntities)
        {
            TryKnockOffMask(xeno, target, args.BaseDamage.GetTotal().Float());
        }
    }

    private void TryKnockOffMask(Entity<STXenoMaskKnockoffComponent> xeno, EntityUid target, float damage)
    {
        if (!_inventory.TryGetSlotEntity(target, "mask", out var mask) ||
            !HasComp<HunterVisionMaskComponent>(mask))
        {
            return;
        }

        var chance = xeno.Comp.BaseChance;

        if (TryComp<XenoFrenzyPheromonesComponent>(xeno.Owner, out var frenzy))
            chance += xeno.Comp.FrenzyChancePerMultiplier * (float) frenzy.Multiplier;

        if (HasComp<STPredalienComponent>(xeno.Owner))
            chance += xeno.Comp.IntelligentCasteBonusChance;

        chance += Math.Min(MathF.Floor(damage / xeno.Comp.DamageChanceDivisor), xeno.Comp.MaxDamageBonusChance);

        if (_mobState.IsIncapacitated(target))
            chance = xeno.Comp.IncapacitatedChance;
        else
            chance = Math.Min(chance, xeno.Comp.MaxChance);

        if (!_random.Prob(chance / 100f))
            return;

        if (!_inventory.TryUnequip(target, target, "mask", force: true))
            return;

        _popup.PopupEntity(
            Loc.GetString("rmc-xeno-mask-knockoff", ("xeno", xeno.Owner), ("target", target)),
            target,
            PopupType.MediumCaution
        );
    }
}
