using Content.Shared._RMC14.Camera;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Camera;

public sealed class RMCCameraSystem : SharedRMCCameraSystem
{
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly Content.Server._Stories.ZLevels.Core.ZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<ActorComponent> _actorQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeLocalEvent<RMCCameraWatcherComponent, PlayerAttachedEvent>(OnWatcherPlayerAttached);
        SubscribeLocalEvent<RMCCameraWatcherComponent, PlayerDetachedEvent>(OnWatcherPlayerDetached);
    }

    private void OnWatcherPlayerAttached(Entity<RMCCameraWatcherComponent> ent, ref PlayerAttachedEvent args)
    {
        foreach (var netOverride in ent.Comp.Overrides)
        {
            if (TryGetEntity(netOverride, out var over))
                _viewSubscriber.AddViewSubscriber(over.Value, args.Player);
        }
    }

    private void OnWatcherPlayerDetached(Entity<RMCCameraWatcherComponent> ent, ref PlayerDetachedEvent args)
    {
        foreach (var netOverride in ent.Comp.Overrides)
        {
            // Stories-ZLevels-Start
            if (!TryGetEntity(netOverride, out var over))
                continue;

            _viewSubscriber.RemoveViewSubscriber(over.Value, args.Player);
            if (MetaData(over.Value).EntityPrototype?.ID == "STZLevelEye")
                QueueDel(over.Value);
            // Stories-ZLevels-End
        }
    }

    protected override void Refresh(Entity<RMCCameraComputerComponent> ent, EntityUid? old)
    {
        base.Refresh(ent, old);

        for (var i = ent.Comp.Watchers.Count - 1; i >= 0; i--)
        {
            var watcher = ent.Comp.Watchers[i];
            if (TerminatingOrDeleted(watcher))
            {
                ent.Comp.Watchers.RemoveAt(i);
                continue;
            }

            if (!_actorQuery.TryComp(watcher, out var actor))
                continue;

            RMCCameraWatcherComponent? watcherComp = null;
            if (old != null && TryComp(watcher, out watcherComp))
                RemoveOverrides((watcher, watcherComp, actor));

            if (ent.Comp.CurrentCamera is not { } current)
                continue;

            _viewSubscriber.AddViewSubscriber(current, actor.PlayerSession);

            watcherComp ??= EnsureComp<RMCCameraWatcherComponent>(watcher);
            watcherComp.Overrides.Add(GetNetEntity(current));

            // Stories: also subscribe to Z-levels below/above camera so they stream via PVS
            if (TryComp<TransformComponent>(current, out var camXform) &&
                camXform.MapUid is { } camMapUid &&
                _zLevels.TryGetMapNetwork(camMapUid, out _))
            {
                var camWorldPos = _transform.GetWorldPosition(camXform);
                for (var offset = 1; offset <= Content.Shared._Stories.ZLevels.Core.EntitySystems.SharedZLevelsSystem.MaxZLevelsBelowRendering; offset++)
                {
                    if (!_zLevels.TryMapOffset(camMapUid, -offset, out var mapBelow))
                        break;

                    var eye = SpawnAtPosition("STZLevelEye", new Robust.Shared.Map.EntityCoordinates(mapBelow, camWorldPos));
                    Transform(eye).GridTraversal = false;
                    _viewSubscriber.AddViewSubscriber(eye, actor.PlayerSession);
                    watcherComp.Overrides.Add(GetNetEntity(eye));
                }

                for (var offset = 1; offset <= Content.Shared._Stories.ZLevels.Core.EntitySystems.SharedZLevelsSystem.MaxZLevelsAboveRendering; offset++)
                {
                    if (!_zLevels.TryMapOffset(camMapUid, offset, out var mapAbove))
                        break;

                    var eye = SpawnAtPosition("STZLevelEye", new Robust.Shared.Map.EntityCoordinates(mapAbove, camWorldPos));
                    Transform(eye).GridTraversal = false;
                    _viewSubscriber.AddViewSubscriber(eye, actor.PlayerSession);
                    watcherComp.Overrides.Add(GetNetEntity(eye));
                }
            }

            Dirty(watcher, watcherComp);
        }
    }

    protected override void OnWatcherRemoved(Entity<RMCCameraWatcherComponent> watcher)
    {
        base.OnWatcherRemoved(watcher);
        RemoveOverrides(watcher);
    }

    private void RemoveOverrides(Entity<RMCCameraWatcherComponent, ActorComponent?> watcher)
    {
        if (!_actorQuery.Resolve(watcher, ref watcher.Comp2, false))
        {
            watcher.Comp1.Overrides.Clear();
            return;
        }

        foreach (var compOverride in watcher.Comp1.Overrides)
        {
            if (!TryGetEntity(compOverride, out var over))
                continue;

            _viewSubscriber.RemoveViewSubscriber(over.Value, watcher.Comp2.PlayerSession);
            // Stories-ZLevels-Start
            if (MetaData(over.Value).EntityPrototype?.ID == "STZLevelEye")
                QueueDel(over.Value);
            // Stories-ZLevels-End
        }

        watcher.Comp1.Overrides.Clear();
        Dirty(watcher, watcher.Comp1);
    }
}
