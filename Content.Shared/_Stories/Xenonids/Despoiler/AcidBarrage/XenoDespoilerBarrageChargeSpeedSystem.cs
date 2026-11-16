using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerBarrageChargeSpeedSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoDespoilerChargingBarrageComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<XenoDespoilerChargingBarrageComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<XenoDespoilerChargingBarrageComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<XenoDespoilerArmedBarrageComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(Entity<XenoDespoilerArmedBarrageComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnStartup(EntityUid uid, XenoDespoilerChargingBarrageComponent comp, ComponentStartup args)
    {
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnShutdown(EntityUid uid, XenoDespoilerChargingBarrageComponent comp, ComponentShutdown args)
    {
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefresh(EntityUid uid, XenoDespoilerChargingBarrageComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        var mult = comp.SpeedMultiplier <= 0 ? 0.5f : comp.SpeedMultiplier;
        args.ModifySpeed(mult, mult);
    }
}
