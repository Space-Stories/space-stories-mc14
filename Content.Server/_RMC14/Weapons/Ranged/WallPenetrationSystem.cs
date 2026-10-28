using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Projectiles;
using Content.Shared.Construction.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Components; // Стандартный компонент физики в Robust Toolbox
using Robust.Shared.Physics.Events;

namespace Content.Server._RMC14.Weapons.Ranged;

public sealed class WallPenetrationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WallPenetratorComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(EntityUid uid, WallPenetratorComponent component, ref PreventCollideEvent args)
    {
        if (component.PenetrationPower <= 0)
            return;

        var target = args.OtherEntity;

        if (!IsPenetratableTarget(target))
            return;

        if (!TryComp<ProjectileComponent>(uid, out var projectile))
            return;

        // Разрешаем пуле лететь сквозь объект
        args.Cancelled = true;

        // Уменьшаем пробитие и режем урон
        component.PenetrationPower--;
        projectile.Damage *= component.DamageMultiplier;

        // Если лимит пробитий исчерпан, снимаем компонент
        if (component.PenetrationPower <= 0)
        {
            RemCompDeferred<WallPenetratorComponent>(uid);
        }
    }

    private bool IsPenetratableTarget(EntityUid target)
    {
        // Игроков и мобов не пробиваем
        if (HasComp<MobStateComponent>(target))
            return false;

        // Пробиваем прикручиваемые объекты (баррикады, укрепления и т.д.)
        if (HasComp<AnchorableComponent>(target))
            return true;

        // Пробиваем любые физические объекты (стены, преграды с твердым телом)
        if (HasComp<PhysicsComponent>(target))
            return true;

        return false;
    }
}