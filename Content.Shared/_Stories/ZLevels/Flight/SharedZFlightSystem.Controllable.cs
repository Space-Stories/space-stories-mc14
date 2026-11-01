using Content.Shared._Stories.ZLevels.Flight.Components;
using Content.Shared.DoAfter;
using Content.Shared.Toggleable;

namespace Content.Shared._Stories.ZLevels.Flight;

public abstract partial class SharedZFlightSystem
{
    private void InitializeControllable()
    {
        SubscribeLocalEvent<ControllableFlightComponent, ZFlightActionUp>(OnZLevelUp);
        SubscribeLocalEvent<ControllableFlightComponent, ZFlightActionDown>(OnZLevelDown);
        SubscribeLocalEvent<ControllableFlightComponent, ToggleActionEvent>(OnZLevelToggle);

        SubscribeLocalEvent<ControllableFlightComponent, StartFlightDoAfterEvent>(OnStartFlightDoAfter);
        SubscribeLocalEvent<ControllableFlightComponent, FlightStartedEvent>(OnControllableFlightStarted);
        SubscribeLocalEvent<ControllableFlightComponent, FlightStoppedEvent>(OnControllableFlightStopped);
    }

    private void OnControllableFlightStopped(Entity<ControllableFlightComponent> ent, ref FlightStoppedEvent args)
    {
        _actions.SetEnabled(ent.Comp.ZLevelDownActionEntity, false);
        _actions.SetEnabled(ent.Comp.ZLevelUpActionEntity, false);

        // Update toggle action icon state
        if (ent.Comp.ZLevelToggleActionEntity != null)
            _actions.SetToggled(ent.Comp.ZLevelToggleActionEntity, false);
    }

    private void OnControllableFlightStarted(Entity<ControllableFlightComponent> ent, ref FlightStartedEvent args)
    {
        _actions.SetEnabled(ent.Comp.ZLevelDownActionEntity, true);
        _actions.SetEnabled(ent.Comp.ZLevelUpActionEntity, true);

        // Update toggle action icon state
        if (ent.Comp.ZLevelToggleActionEntity != null)
            _actions.SetToggled(ent.Comp.ZLevelToggleActionEntity, true);
    }

    private void OnZLevelUp(Entity<ControllableFlightComponent> ent, ref ZFlightActionUp args)
    {
        if (args.Handled)
            return;

        var map = Transform(ent).MapUid;
        if (map is null)
            return;

        if (!TryComp<ZFlyerComponent>(ent, out var flyerComp))
            return;

        if (!_zLevel.TryMapUp(map.Value, out var mapAbove))
            return;

        flyerComp.TargetMapHeight = mapAbove.Comp.Depth;
        Dirty(ent, flyerComp);

        args.Handled = true;
    }

    private void OnZLevelDown(Entity<ControllableFlightComponent> ent, ref ZFlightActionDown args)
    {
        if (args.Handled)
            return;

        var map = Transform(ent).MapUid;
        if (map is null)
            return;

        if (!TryComp<ZFlyerComponent>(ent, out var flyerComp))
            return;

        if (!_zLevel.TryMapDown(map.Value, out var mapBelow))
            return;

        flyerComp.TargetMapHeight = mapBelow.Comp.Depth;
        Dirty(ent, flyerComp);

        args.Handled = true;
    }

    private void OnZLevelToggle(Entity<ControllableFlightComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Action.Owner != ent.Comp.ZLevelToggleActionEntity)
            return;

        if (!TryComp<ZFlyerComponent>(ent, out var flyerComp))
            return;

        if (flyerComp.Active)
        {
            DeactivateFlight((ent, flyerComp));
        }
        else
        {
            // If StartFlightDoAfter is set, start a doAfter before activating flight
            if (ent.Comp.StartFlightDoAfter != null)
            {
                //Preventive start flying visuals
                StartFlightVisuals((ent, flyerComp));

                var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.StartFlightDoAfter.Value, new StartFlightDoAfterEvent(), ent)
                {
                    BreakOnMove = false,
                    BlockDuplicate = true,
                    BreakOnDamage = true,
                    CancelDuplicate = true,
                };

                _doAfter.TryStartDoAfter(doAfter);
            }
            else
            {
                // No delay, activate flight immediately
                TryActivateFlight((ent, flyerComp));
            }
        }

        args.Handled = true;
    }

    private void OnStartFlightDoAfter(Entity<ControllableFlightComponent> ent, ref StartFlightDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            StopFlightVisuals(ent.Owner);
            return;
        }

        TryActivateFlight(ent.Owner);
        args.Handled = true;
    }
}
