using System.Numerics;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Ladder;
using Content.Shared._RMC14.Teleporter;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared._Stories.ZLevels.Pulling;
using Content.Shared.ActionBlocker;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Explosion.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.ZLevels.Ladder;

public abstract class SharedZLevelLadderSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedRMCTeleporterSystem _rmcTeleporter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelLadderComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<ZLevelLadderComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ZLevelLadderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ZLevelLadderComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ZLevelLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<ZLevelLadderComponent, DoAfterAttemptEvent<ZLevelLadderDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<ZLevelLadderComponent, ZLevelLadderDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<ZLevelLadderComponent, ZLevelLadderClimbBuiMessage>(OnBuiClimbMessage);
        SubscribeLocalEvent<ZLevelLadderComponent, CanDropDraggedEvent>(OnCanDropDragged);
        SubscribeLocalEvent<ZLevelLadderComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<ZLevelLadderComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<ZLevelLadderComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<ZLevelLadderComponent, DragDropTargetEvent>(OnDragDropTarget);
    }

    private void OnInteractHand(Entity<ZLevelLadderComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryInteractLadder(ent, args.User);
    }

    private void OnInteractUsing(Entity<ZLevelLadderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryInteractLadder(ent, args.User);
    }

    private void OnActivateInWorld(Entity<ZLevelLadderComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryInteractLadder(ent, args.User);
    }

    private void OnGetVerbs(Entity<ZLevelLadderComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        var hasUp = HasLadderAtDestination(ent.Owner, 1);
        var hasDown = HasLadderAtDestination(ent.Owner, -1);

        if ((ent.Comp.Direction == ZLevelLadderDirection.Up || ent.Comp.Direction == ZLevelLadderDirection.Both) && hasUp)
        {
            var verbUp = new InteractionVerb
            {
                Text = Loc.GetString("stories-zlevel-ladder-verb-up"),
                Act = () => StartClimbDoAfter(ent, user, true),
                Icon = new SpriteSpecifier.Rsi(new ResPath("_Stories/Interface/ladders.rsi"), "up"),
            };
            args.Verbs.Add(verbUp);
        }

        if ((ent.Comp.Direction == ZLevelLadderDirection.Down || ent.Comp.Direction == ZLevelLadderDirection.Both) && hasDown)
        {
            var verbDown = new InteractionVerb
            {
                Text = Loc.GetString("stories-zlevel-ladder-verb-down"),
                Act = () => StartClimbDoAfter(ent, user, false),
                Icon = new SpriteSpecifier.Rsi(new ResPath("_Stories/Interface/ladders.rsi"), "down"),
            };
            args.Verbs.Add(verbDown);
        }

        // Add verb to throw active held item (e.g. grenade) up or down ladder
        if (_hands.TryGetActiveItem(user, out var heldItem))
        {
            if ((ent.Comp.Direction == ZLevelLadderDirection.Up || ent.Comp.Direction == ZLevelLadderDirection.Both) && hasUp)
            {
                args.Verbs.Add(new InteractionVerb
                {
                    Text = Loc.GetString("stories-zlevel-ladder-throw-up"),
                    Act = () => ThrowItemThroughLadder(ent, user, heldItem.Value, true),
                    Priority = -1,
                });
            }

            if ((ent.Comp.Direction == ZLevelLadderDirection.Down || ent.Comp.Direction == ZLevelLadderDirection.Both) && hasDown)
            {
                args.Verbs.Add(new InteractionVerb
                {
                    Text = Loc.GetString("stories-zlevel-ladder-throw-down"),
                    Act = () => ThrowItemThroughLadder(ent, user, heldItem.Value, false),
                    Priority = -2,
                });
            }
        }
    }

    private void OnGetAltVerbs(Entity<ZLevelLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var hasUp = TryGetDestinationLadder(ent.Owner, 1, out var destUp);
        var hasDown = TryGetDestinationLadder(ent.Owner, -1, out var destDown);

        if ((ent.Comp.Direction == ZLevelLadderDirection.Up || ent.Comp.Direction == ZLevelLadderDirection.Both) && hasUp)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Priority = 100,
                Text = Loc.GetString("stories-zlevel-ladder-look-through-up"),
                Act = () => Watch(user, destUp),
            });
        }

        if ((ent.Comp.Direction == ZLevelLadderDirection.Down || ent.Comp.Direction == ZLevelLadderDirection.Both) && hasDown)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Priority = 99,
                Text = Loc.GetString("stories-zlevel-ladder-look-through-down"),
                Act = () => Watch(user, destDown),
            });
        }
    }

    public bool TryGetDestinationLadder(EntityUid ladder, int offset, out Entity<ZLevelLadderComponent> destLadder)
    {
        destLadder = default;
        var xform = Transform(ladder);
        if (xform.MapUid is not { } mapUid)
            return false;

        if (!_zLevel.TryMapOffset(mapUid, offset, out var targetMapUid))
            return false;

        var worldPos = _transform.GetWorldPosition(ladder);
        if (!_mapManager.TryFindGridAt(targetMapUid, worldPos, out var destGridUid, out var destGrid))
        {
            if (TryComp<MapGridComponent>(targetMapUid, out destGrid))
                destGridUid = targetMapUid;
            else
                return false;
        }

        var tileIndices = _map.WorldToTile(destGridUid, destGrid, worldPos);
        var enumerator = _map.GetAnchoredEntitiesEnumerator(destGridUid, destGrid, tileIndices);
        while (enumerator.MoveNext(out var uid))
        {
            if (uid is not { } ladderUid)
                continue;

            if (TryComp<ZLevelLadderComponent>(ladderUid, out var destComp))
            {
                if (offset > 0 && destComp.Direction != ZLevelLadderDirection.Down && destComp.Direction != ZLevelLadderDirection.Both)
                    continue;
                if (offset < 0 && destComp.Direction != ZLevelLadderDirection.Up && destComp.Direction != ZLevelLadderDirection.Both)
                    continue;

                destLadder = (ladderUid, destComp);
                return true;
            }
        }

        return false;
    }

    public bool HasLadderAtDestination(EntityUid entity, int offset)
    {
        return TryGetDestinationLadder(entity, offset, out _);
    }

    private bool TryInteractLadder(Entity<ZLevelLadderComponent> ent, EntityUid user)
    {
        var hasUp = HasLadderAtDestination(ent.Owner, 1);
        var hasDown = HasLadderAtDestination(ent.Owner, -1);

        if (HasComp<GhostComponent>(user))
        {
            // Ghosts teleport instantly
            if (ent.Comp.Direction == ZLevelLadderDirection.Up && TryGetDestinationLadder(ent.Owner, 1, out var destUp))
            {
                if (_zLevel.TryMoveUp(user))
                    _transform.SetCoordinates(user, Transform(destUp.Owner).Coordinates);
            }
            else if (ent.Comp.Direction == ZLevelLadderDirection.Down && TryGetDestinationLadder(ent.Owner, -1, out var destDown))
            {
                if (_zLevel.TryMoveDown(user))
                    _transform.SetCoordinates(user, Transform(destDown.Owner).Coordinates);
            }
            else if (ent.Comp.Direction == ZLevelLadderDirection.Both && (hasUp || hasDown))
            {
                if (_net.IsServer)
                {
                    EnsureComp<UserInterfaceComponent>(ent);
                    _ui.TryOpenUi(ent.Owner, ZLevelLadderUiKey.Key, user);
                    ent.Comp.HasUp = hasUp;
                    ent.Comp.HasDown = hasDown;
                    Dirty(ent);
                }
            }
            return true;
        }

        if (ent.Comp.Direction == ZLevelLadderDirection.Both)
        {
            if (!hasUp && !hasDown)
            {
                if (_net.IsServer)
                {
                    var msg = Loc.GetString("stories-zlevel-ladder-cannot-move");
                    _popup.PopupEntity(msg, ent, user, PopupType.SmallCaution);
                }
                return true;
            }

            if (_net.IsServer)
            {
                EnsureComp<UserInterfaceComponent>(ent);
                _ui.TryOpenUi(ent.Owner, ZLevelLadderUiKey.Key, user);
                ent.Comp.HasUp = hasUp;
                    ent.Comp.HasDown = hasDown;
                    Dirty(ent);
            }
            return true;
        }

        var goUp = ent.Comp.Direction == ZLevelLadderDirection.Up;
        var canMove = goUp ? hasUp : hasDown;

        if (!canMove)
        {
            if (_net.IsServer)
            {
                var textKey = goUp ? "stories-zlevel-ladder-no-level-up" : "stories-zlevel-ladder-no-level-down";
                var msg = Loc.GetString(textKey);
                _popup.PopupEntity(msg, ent, user, PopupType.SmallCaution);
            }
            return true;
        }

        StartClimbDoAfter(ent, user, goUp);
        return true;
    }

    private void OnBuiClimbMessage(Entity<ZLevelLadderComponent> ent, ref ZLevelLadderClimbBuiMessage args)
    {
        StartClimbDoAfter(ent, args.Actor, args.Up);
    }

    private void OnDoAfterAttempt(Entity<ZLevelLadderComponent> ent, ref DoAfterAttemptEvent<ZLevelLadderDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        var user = args.DoAfter.Args.User;
        var target = ent.Owner.ToCoordinates();
        if (user.ToCoordinates().TryDistance(EntityManager, _transform, target, out var distance) &&
            distance > ent.Comp.Range)
        {
            args.Cancel();
        }

        if (Transform(user).Anchored)
            args.Cancel();
    }

    public void StartClimbDoAfter(Entity<ZLevelLadderComponent> ent, EntityUid user, bool goUp)
    {
        var offset = goUp ? 1 : -1;
        if (!HasLadderAtDestination(ent.Owner, offset))
        {
            if (_net.IsServer)
            {
                var msg = Loc.GetString("stories-zlevel-ladder-no-destination");
                _popup.PopupEntity(msg, ent, user, PopupType.SmallCaution);
            }
            return;
        }

        var time = _timing.CurTime;
        if (ent.Comp.LastDoAfterEnt is { } lastEnt &&
            ent.Comp.LastDoAfterId is { } lastId &&
            time - ent.Comp.LastDoAfterTime < ent.Comp.Delay * 5 &&
            _doAfter.GetStatus(new DoAfterId(lastEnt, lastId)) == DoAfterStatus.Running &&
            !HasComp<GhostComponent>(user))
        {
            if (ent.Comp.LastDoAfterEnt != user && _net.IsServer)
            {
                var msg = Loc.GetString("rmc-ladder-someone-else-climbing");
                _popup.PopupEntity(msg, ent, user, PopupType.SmallCaution);
            }

            return;
        }

        var delay = ent.Comp.Delay;
        if (HasComp<GhostComponent>(user))
            delay = TimeSpan.Zero;

        var ev = new ZLevelLadderDoAfterEvent(goUp);
        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, ev, ent, ent, ent)
        {
            AttemptFrequency = delay == TimeSpan.Zero ? AttemptFrequency.Never : AttemptFrequency.EveryTick,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = ent.Comp.Range,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return;

        ent.Comp.LastDoAfterEnt = doAfterId.Value.Uid;
        ent.Comp.LastDoAfterId = doAfterId.Value.Index;
        ent.Comp.LastDoAfterTime = time;
        Dirty(ent);

        if (delay > TimeSpan.Zero && _net.IsServer)
        {
            var actionText = Loc.GetString(goUp ? "stories-zlevel-ladder-action-climb-up" : "stories-zlevel-ladder-action-climb-down");
            var selfMessage = Loc.GetString("stories-zlevel-ladder-climb-start", ("action", actionText));
            var othersMessage = Loc.GetString("rmc-ladder-start-climbing-others", ("user", user));
            _popup.PopupPredicted(selfMessage, othersMessage, user, user);
        }
    }

    private void OnDoAfter(Entity<ZLevelLadderComponent> ent, ref ZLevelLadderDoAfterEvent args)
    {
        var user = args.User;

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var offset = args.Up ? 1 : -1;
        if (!HasLadderAtDestination(ent.Owner, offset))
        {
            if (_net.IsServer)
            {
                var msg = Loc.GetString("stories-zlevel-ladder-no-destination");
                _popup.PopupEntity(msg, ent, user, PopupType.SmallCaution);
            }
            return;
        }

        var hasPairedLadder = TryGetDestinationLadder(ent.Owner, offset, out var destLadder);

        // Check if user is carrying someone or pulling an entity
        EntityUid? carriedEnt = null;
        if (TryComp<CanFiremanCarryComponent>(user, out var carrierComp) && carrierComp.Carrying is { } c && Exists(c))
        {
            carriedEnt = c;
            EnsureComp<ZLevelTransitioningComponent>(user);
            EnsureComp<ZLevelTransitioningComponent>(c);
        }

        EntityUid? pulledEnt = null;
        if (TryComp<PullerComponent>(user, out var puller) &&
            puller.Pulling is { } pulling &&
            pulling != carriedEnt &&
            TryComp<PullableComponent>(pulling, out var pulledComp))
        {
            pulledEnt = pulling;
            EnsureComp<ZLevelTransitioningComponent>(user);
            EnsureComp<ZLevelTransitioningComponent>(pulling);
            _pulling.RemovePullJointDuringTransition(pulling, pulledComp);
        }

        bool moved;
        if (args.Up)
            moved = _zLevel.TryMoveUp(user);
        else
            moved = _zLevel.TryMoveDown(user);

        if (moved)
        {
            if (TryComp<Content.Shared._Stories.ZLevels.Core.Components.ZPhysicsComponent>(user, out var userZPhys))
            {
                userZPhys.LocalPosition = 0f;
                userZPhys.Velocity = 0f;
                userZPhys.CachedGroundHeight = 0f;
                userZPhys.CachedStickyGround = false;
                userZPhys.VisualZApplied = false;
                DirtyField(user, userZPhys, nameof(ZPhysicsComponent.LocalPosition));
                DirtyField(user, userZPhys, nameof(ZPhysicsComponent.Velocity));
            }

            var destCoords = hasPairedLadder ? Transform(destLadder.Owner).Coordinates : Transform(user).Coordinates;
            if (hasPairedLadder)
            {
                var ladderWorldPos = _transform.GetWorldPosition(destLadder.Owner);
                var targetMapUid = Transform(destLadder.Owner).MapUid;
                if (targetMapUid != null &&
                    _zLevel.TryFindGridAt(targetMapUid.Value, ladderWorldPos, out var gUid, out var grid))
                {
                    var ladderTile = _map.WorldToTile(gUid, grid, ladderWorldPos);
                    if (_turf.IsTileBlocked(gUid, ladderTile, CollisionGroup.Impassable | CollisionGroup.MidImpassable, grid))
                    {
                        var ladderRot = _transform.GetWorldRotation(destLadder.Owner);
                        var stepDir = ladderRot.ToWorldVec();
                        if (stepDir == Vector2.Zero)
                            stepDir = new Vector2(0, -1);

                        var stepPos = ladderWorldPos + stepDir;
                        if (!_zLevel.IsTileTransparentOrEmpty(targetMapUid.Value, stepPos))
                        {
                            var tile = _map.WorldToTile(gUid, grid, stepPos);
                            if (!_turf.IsTileBlocked(gUid, tile, CollisionGroup.Impassable | CollisionGroup.MidImpassable, grid))
                            {
                                destCoords = _transform.ToCoordinates(destLadder.Owner, new MapCoordinates(stepPos, Transform(destLadder.Owner).MapID));
                            }
                        }
                    }
                }

                _transform.SetCoordinates(user, destCoords);
            }

            if (carriedEnt is { } carried && Exists(carried))
            {
                _transform.SetCoordinates(carried, destCoords);
                _transform.SetParent(carried, user);
                _transform.SetLocalPosition(carried, Vector2.Zero);
                if (TryComp<ZPhysicsComponent>(carried, out var carriedZPhys))
                {
                    carriedZPhys.LocalPosition = 0f;
                    carriedZPhys.Velocity = 0f;
                    carriedZPhys.CachedGroundHeight = 0f;
                    carriedZPhys.CachedStickyGround = false;
                    carriedZPhys.VisualZApplied = false;
                    DirtyField(carried, carriedZPhys, nameof(ZPhysicsComponent.LocalPosition));
                    DirtyField(carried, carriedZPhys, nameof(ZPhysicsComponent.Velocity));
                }
                RemCompDeferred<ZLevelTransitioningComponent>(carried);
            }

            if (pulledEnt is { } pulled && Exists(pulled))
            {
                var pulledCoords = destCoords;
                if (hasPairedLadder)
                {
                    var ladderWorldPos = _transform.GetWorldPosition(destLadder.Owner);
                    var targetMapUid = Transform(destLadder.Owner).MapUid;
                    if (targetMapUid != null &&
                        _zLevel.TryFindGridAt(targetMapUid.Value, ladderWorldPos, out var lGridUid, out var lGrid))
                    {
                        var lTile = _map.WorldToTile(lGridUid, lGrid, ladderWorldPos);
                        if (_turf.IsTileBlocked(lGridUid, lTile, CollisionGroup.Impassable | CollisionGroup.MidImpassable, lGrid))
                        {
                            pulledCoords = Transform(user).Coordinates;
                        }
                    }
                }

                _transform.SetCoordinates(pulled, pulledCoords);
                if (TryComp<ZPhysicsComponent>(pulled, out var pulledZPhys))
                {
                    pulledZPhys.LocalPosition = 0f;
                    pulledZPhys.Velocity = 0f;
                    pulledZPhys.CachedGroundHeight = 0f;
                    pulledZPhys.CachedStickyGround = false;
                    pulledZPhys.VisualZApplied = false;
                    DirtyField(pulled, pulledZPhys, nameof(ZPhysicsComponent.LocalPosition));
                    DirtyField(pulled, pulledZPhys, nameof(ZPhysicsComponent.Velocity));
                }
                if (TryComp<PhysicsComponent>(pulled, out var pullPhysics) && pullPhysics.BodyType != BodyType.Static)
                {
                    _physics.WakeBody(pulled, body: pullPhysics);
                    _physics.SetAwake(pulled, pullPhysics, true);
                }

                _pulling.TryStartPull(user, pulled);
                RemCompDeferred<ZLevelTransitioningComponent>(pulled);
            }

            if (carriedEnt != null || pulledEnt != null)
                RemCompDeferred<ZLevelTransitioningComponent>(user);

            if (hasPairedLadder && _net.IsServer && destLadder.Comp.Direction == ZLevelLadderDirection.Both)
            {
                var destHasUp = HasLadderAtDestination(destLadder.Owner, 1);
                var destHasDown = HasLadderAtDestination(destLadder.Owner, -1);
                if (destHasUp || destHasDown)
                {
                    EnsureComp<UserInterfaceComponent>(destLadder.Owner);
                    _ui.TryOpenUi(destLadder.Owner, ZLevelLadderUiKey.Key, user);
                    destLadder.Comp.HasUp = destHasUp;
                    destLadder.Comp.HasDown = destHasDown;
                    Dirty(destLadder);
                }
            }

            if (_net.IsServer)
            {
                var finishText = Loc.GetString(args.Up ? "stories-zlevel-ladder-climb-finish-up" : "stories-zlevel-ladder-climb-finish-down");
                var othersMessage = Loc.GetString("rmc-ladder-finish-climbing-others", ("user", user));
                _popup.PopupPredicted(finishText, othersMessage, user, user);
            }


        }

        ent.Comp.LastDoAfterEnt = null;
        ent.Comp.LastDoAfterId = null;
        Dirty(ent);
    }

    private void ThrowItemThroughLadder(Entity<ZLevelLadderComponent> ent, EntityUid user, EntityUid item, bool up)
    {
        var offset = up ? 1 : -1;
        if (!HasLadderAtDestination(ent.Owner, offset))
        {
            if (_net.IsServer)
            {
                var msg = Loc.GetString("stories-zlevel-ladder-no-destination");
                _popup.PopupEntity(msg, ent, user, PopupType.SmallCaution);
            }
            return;
        }

        // Prime timer trigger if item has OnUseTimerTriggerComponent
        if (HasComp<OnUseTimerTriggerComponent>(item))
        {
            var useEv = new UseInHandEvent(user);
            RaiseLocalEvent(item, useEv);
        }

        // Drop item from user's hand
        _hands.TryDrop(user, item);

        // Move item to destination Z-level
        if (_zLevel.TryMove(item, offset))
        {
            if (TryGetDestinationLadder(ent.Owner, offset, out var destLadder))
            {
                var destCoords = Transform(destLadder.Owner).Coordinates;
                _transform.SetCoordinates(item, destCoords);
            }

            if (_net.IsServer)
            {
                var sound = new SoundPathSpecifier("/Audio/Effects/throw.ogg");
                _audio.PlayPvs(sound, ent);
                var selfMsg = Loc.GetString(up ? "stories-zlevel-ladder-threw-up-self" : "stories-zlevel-ladder-threw-down-self");
                _popup.PopupPredicted(selfMsg, user, user);
            }
        }
    }

    protected virtual void Watch(Entity<ActorComponent?, EyeComponent?> watcher, Entity<ZLevelLadderComponent> toWatch)
    {
    }

    protected virtual void Unwatch(Entity<EyeComponent?> watcher, ICommonSession player)
    {
        if (!Resolve(watcher, ref watcher.Comp))
            return;

        _eye.SetTarget(watcher, null);
    }

    private void OnCanDropDragged(Entity<ZLevelLadderComponent> ent, ref CanDropDraggedEvent args)
    {
        if (args.User != args.Target)
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnCanDrag(Entity<ZLevelLadderComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnDragDropDragged(Entity<ZLevelLadderComponent> ent, ref DragDropDraggedEvent args)
    {
        var user = args.User;
        if (args.Target != user)
            return;

        args.Handled = TryInteractLadder(ent, user);
    }

    private void OnCanDropTarget(Entity<ZLevelLadderComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (!_actionBlocker.CanInteract(args.User, args.Dragged) ||
            !_actionBlocker.CanInteract(args.User, ent.Owner))
        {
            return;
        }

        if (Transform(args.Dragged).Anchored)
            return;

        args.CanDrop = true;
        args.Handled = true;
    }

    private void OnDragDropTarget(Entity<ZLevelLadderComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (!_actionBlocker.CanInteract(args.User, args.Dragged) ||
            !_actionBlocker.CanInteract(args.User, ent.Owner))
        {
            return;
        }

        if (Transform(args.Dragged).Anchored)
            return;

        args.Handled = TrySendEntityThroughLadder(ent, args.Dragged, args.User);
    }

    public bool TrySendEntityThroughLadder(Entity<ZLevelLadderComponent> ent, EntityUid dragged, EntityUid user)
    {
        var offset = ent.Comp.Direction == ZLevelLadderDirection.Down ? -1 : 1;
        if (ent.Comp.Direction == ZLevelLadderDirection.Both)
            offset = HasLadderAtDestination(ent.Owner, 1) ? 1 : -1;

        var hasPairedLadder = TryGetDestinationLadder(ent.Owner, offset, out var destLadder);

        EnsureComp<ZLevelTransitioningComponent>(dragged);

        var moved = offset > 0 ? _zLevel.TryMoveUp(dragged) : _zLevel.TryMoveDown(dragged);

        if (moved)
        {
            if (hasPairedLadder)
            {
                var ladderWorldPos = _transform.GetWorldPosition(destLadder.Owner);
                var ladderRot = _transform.GetWorldRotation(destLadder.Owner);
                var stepDir = ladderRot.ToWorldVec();
                if (stepDir == Vector2.Zero)
                    stepDir = new Vector2(0, -1);

                var stepPos = ladderWorldPos + stepDir;
                var targetMapUid = Transform(destLadder.Owner).MapUid;
                var destCoords = Transform(destLadder.Owner).Coordinates;
                if (targetMapUid != null &&
                    !_zLevel.IsTileTransparentOrEmpty(targetMapUid.Value, stepPos) &&
                    _zLevel.TryFindGridAt(targetMapUid.Value, stepPos, out var gUid, out var grid))
                {
                    var tile = _map.WorldToTile(gUid, grid, stepPos);
                    var ladderTile = _map.WorldToTile(gUid, grid, ladderWorldPos);
                    if (_turf.IsTileBlocked(gUid, ladderTile, CollisionGroup.Impassable | CollisionGroup.MidImpassable, grid) &&
                        !_turf.IsTileBlocked(gUid, tile, CollisionGroup.Impassable | CollisionGroup.MidImpassable, grid))
                    {
                        destCoords = _transform.ToCoordinates(destLadder.Owner, new MapCoordinates(stepPos, Transform(destLadder.Owner).MapID));
                    }
                }
                _transform.SetCoordinates(dragged, destCoords);
            }

            if (TryComp<ZPhysicsComponent>(dragged, out var zPhys))
            {
                zPhys.LocalPosition = 0f;
                zPhys.Velocity = 0f;
                zPhys.CachedGroundHeight = 0f;
                zPhys.CachedStickyGround = false;
                zPhys.VisualZApplied = false;
                DirtyField(dragged, zPhys, nameof(ZPhysicsComponent.LocalPosition));
                DirtyField(dragged, zPhys, nameof(ZPhysicsComponent.Velocity));
            }
        }

        RemCompDeferred<ZLevelTransitioningComponent>(dragged);
        return moved;
    }
}
