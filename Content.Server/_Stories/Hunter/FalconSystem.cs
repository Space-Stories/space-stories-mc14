using Content.Shared.Actions;
using Content.Shared.Hunter.FalconComponent;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared._RMC14.Actions;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Player;
using Content.Shared._Stories.Hunter.Falcon;

namespace Content.Server._Stories.Hunter.Falcon;

public sealed class FalconSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FalconDroneComponent, UseInHandEvent>(OnFalconDroneUse);
        SubscribeLocalEvent<FalconDroneComponent, GetItemActionsEvent>(OnFalconDroneGetItemActions);

        SubscribeLocalEvent<FalconDroneComponent, FalconControlActionEvent>(OnFalconControl);
        SubscribeLocalEvent<FalconDroneComponent, FalconRecallActionEvent>(OnFalconRecall);

        SubscribeLocalEvent<FalconDroneComponent, MobStateChangedEvent>(OnControllerMobStateChanged);

        SubscribeLocalEvent<FalconDroneComponent, EntityTerminatingEvent>(OnDroneTerminating);
    }
    private void OnFalconDroneUse(Entity<FalconDroneComponent> drone, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryToggleFalconControl(drone, args.User);
    }

    private void OnFalconDroneGetItemActions(Entity<FalconDroneComponent> drone, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ref drone.Comp.RecallAction, drone.Comp.RecallActionId);

        if (args.SlotFlags != null && (args.SlotFlags.Value & SlotFlags.EARS) != 0)
        {
            args.AddAction(ref drone.Comp.ControlAction, drone.Comp.ControlActionId);
        }
    }
    private void OnFalconControl(Entity<FalconDroneComponent> drone, ref FalconControlActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryToggleFalconControl(drone, args.Performer);
    }
    private void TryToggleFalconControl(Entity<FalconDroneComponent> drone, EntityUid user)
    {
        if (!_actionBlocker.CanConsciouslyPerformAction(user))
            return;

        if (drone.Comp.Controller == user)
        {
            ReturnDrone(drone);
            return;
        }

        DeployFalconDrone(drone, user);
    }

    private void OnFalconRecall(Entity<FalconDroneComponent> drone, ref FalconRecallActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        if (drone.Comp.Controller == args.Performer)
        {
            ReturnDrone(drone);
            _popup.PopupEntity(Loc.GetString("stories-falcon-drone-recalled"), args.Performer, args.Performer);
        }
    }

    private void DeployFalconDrone(Entity<FalconDroneComponent> drone, EntityUid user)
    {
        EntityUid? previousEyeTarget = user;
        if (TryComp<EyeComponent>(user, out var eye))
            previousEyeTarget = eye.Target ?? user;

        var deployed = drone.Owner;

        _transform.AttachToGridOrMap(deployed);

        drone.Comp.Controller = user;
        drone.Comp.PreviousEyeTarget = previousEyeTarget;

        EnsureComp<InputMoverComponent>(deployed);
        _mover.SetRelay(user, deployed);
        var interactionRelay = EnsureComp<InteractionRelayComponent>(user);
        _interaction.SetRelay(user, deployed, interactionRelay);

        if (eye != null && TryComp<ActorComponent>(user, out var actor) && actor.PlayerSession != null)
            _eye.SetTarget(user, deployed, eye);

        _popup.PopupEntity(Loc.GetString("stories-falcon-drone-deployed"), user, user);
    }
    private void ReturnDrone(Entity<FalconDroneComponent> drone)
    {
        var user = drone.Comp.Controller;
        var deployed = drone.Owner;

        if (!user.IsValid())
            return;

        if (drone.Comp.PreviousEyeTarget.HasValue)
        {
            if (TryComp<EyeComponent>(user, out var eye))
                _eye.SetTarget(user, drone.Comp.PreviousEyeTarget.Value, eye);
        }

        if (TryComp<InputMoverComponent>(deployed, out _))
            RemComp<InputMoverComponent>(deployed);
        if (TryComp<InteractionRelayComponent>(user, out _))
            RemComp<InteractionRelayComponent>(user);

        drone.Comp.Controller = EntityUid.Invalid;
        drone.Comp.PreviousEyeTarget = null;
    }
    private void OnControllerMobStateChanged(EntityUid uid, FalconDroneComponent component, ref MobStateChangedEvent args)
    {
        if (TryComp<MobStateComponent>(uid, out var mobStateComp) && mobStateComp.CurrentState == MobState.Dead)
        {
            if (component.Controller == uid)
            {
                ReturnDrone((uid, component));
            }
        }
    }
    private void OnDroneTerminating(EntityUid uid, FalconDroneComponent component, ref EntityTerminatingEvent args)
    {
        var controller = component.Controller;

        if (controller.IsValid() && component.PreviousEyeTarget.HasValue)
        {
            if (TryComp<EyeComponent>(controller, out var eye))
                _eye.SetTarget(controller, component.PreviousEyeTarget.Value, eye);
        }

        component.Controller = EntityUid.Invalid;
    }
}
