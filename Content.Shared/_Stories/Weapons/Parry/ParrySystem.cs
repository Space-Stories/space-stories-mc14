using Content.Shared._RMC14.Random;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Weapons.Parry;

public sealed class ParrySystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private (EntityUid Projectile, EntityUid Defender)? _lastProjectileHit;

    public override void Initialize()
    {
        base.Initialize();

        Subs.SubscribeWithRelay<ParryComponent, ProjectileReflectAttemptEvent>(OnProjectileReflectAttempt, baseEvent: false);
        SubscribeLocalEvent<HandsComponent, XenoLeapHitAttempt>(OnXenoLeapHitAttempt);
    }

    private void OnProjectileReflectAttempt(Entity<ParryComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (_net.IsClient || args.Cancelled)
            return;

        var defender = Transform(ent).ParentUid;
        if (!defender.IsValid() || args.Component.Shooter is not { } shooter)
            return;

        if (_lastProjectileHit == (args.ProjUid, defender))
            return;

        _lastProjectileHit = (args.ProjUid, defender);

        if (!TryParry(defender, shooter, args.ProjUid, ParryAttack.Projectile, predicted: false))
            return;

        args.Cancelled = true;
        QueueDel(args.ProjUid);
    }

    private void OnXenoLeapHitAttempt(Entity<HandsComponent> ent, ref XenoLeapHitAttempt args)
    {
        if (args.Cancelled)
            return;

        if (TryParry(ent, args.Leaper, null, ParryAttack.Pounce))
            args.Cancelled = true;
    }

    public bool TryParry(EntityUid defender, EntityUid attacker, EntityUid? weapon, ParryAttack attack, bool predicted = true)
    {
        if (defender == attacker || _mobState.IsIncapacitated(defender))
            return false;

        if (weapon != null && HasComp<UnblockableComponent>(weapon))
            return false;

        if (!TryComp(defender, out HandsComponent? hands))
            return false;

        Entity<ParryComponent>? first = null;
        Entity<ParryComponent>? second = null;
        foreach (var held in _hands.EnumerateHeld((defender, hands)))
        {
            if (!TryComp(held, out ParryComponent? parry))
                continue;

            if (parry.Shield)
            {
                first = (held, parry);
                second = null;
                break;
            }

            if (first == null)
                first = (held, parry);
            else
                second ??= (held, parry);
        }

        if (first is not { } a)
            return false;

        if (attack == ParryAttack.Pounce && !a.Comp.BlockPounce && second is not { Comp.BlockPounce: true })
            return false;

        var chanceA = GetHandChance(a);
        var chanceB = second is { } b ? GetHandChance(b) : 0f;
        var chance = MathF.Max(chanceA + chanceB / 2, chanceB + chanceA / 2);
        if (chance <= 0)
            return false;

        var directional = IsDirectional(a.Comp.BlockType) && (second is not { } s || IsDirectional(s.Comp.BlockType));
        if (directional && !IsFacing(defender, attacker))
            return false;

        if (attack == ParryAttack.Projectile)
        {
            var multiplier = MathF.Max(a.Comp.ProjectileMultiplier, second?.Comp.ProjectileMultiplier ?? 0f);
            chance *= multiplier;
        }

        var seed = ((long) _timing.CurTick.Value << 32) | (uint) GetNetEntity(attacker).Id;
        var roll = new Xoroshiro64S(seed).NextFloat(0, 100);
        if (roll >= chance)
            return false;

        var blocker = second is { } other && chanceB > chanceA ? other : a;
        var message = Loc.GetString("stories-parry-blocked", ("defender", defender), ("weapon", blocker.Owner));
        if (predicted)
        {
            _audio.PlayPredicted(blocker.Comp.Sound, defender, attacker);
            _popup.PopupPredicted(message, defender, attacker, PopupType.SmallCaution);
        }
        else
        {
            _audio.PlayPvs(blocker.Comp.Sound, defender);
            _popup.PopupEntity(message, defender, PopupType.SmallCaution);
        }

        return true;
    }

    private float GetHandChance(Entity<ParryComponent> ent)
    {
        var chance = ent.Comp.Chance;
        if (TryComp(ent, out WieldableComponent? wieldable) && wieldable.Wielded)
            return chance;

        return ent.Comp.BlockType switch
        {
            ParryType.DirectionalTwoHands => chance / 3,
            ParryType.AbsoluteTwoHands => chance / 2,
            _ => chance,
        };
    }

    private static bool IsDirectional(ParryType type)
    {
        return type is ParryType.Directional or ParryType.DirectionalTwoHands;
    }

    private bool IsFacing(EntityUid defender, EntityUid attacker)
    {
        var (defenderPosition, defenderRotation) = _transform.GetWorldPositionRotation(defender);
        var diff = _transform.GetWorldPosition(attacker) - defenderPosition;
        if (diff.LengthSquared() < 0.0001f)
            return true;

        var relative = Math.Abs(diff.Normalized().GetDir() - defenderRotation.GetDir());
        return relative is 0 or 1 or 7;
    }
}
