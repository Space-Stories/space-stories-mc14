using Content.Shared._Stories.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Analyzers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Stories.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem
{
    private readonly List<EntityUid> _activeBodies = new();

    public IReadOnlyList<EntityUid> ActiveBodies => _activeBodies;

    private void InitializeActivation()
    {
        SubscribeLocalEvent<ZPhysicsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZPhysicsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZPhysicsComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<ZPhysicsComponent, PhysicsBodyTypeChangedEvent>(OnPhysicsBodyTypeChanged);
        SubscribeLocalEvent<ZPhysicsComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnMapInit(Entity<ZPhysicsComponent> entity, ref MapInitEvent args)
    {
        RefreshBody(entity);
        SyncCurrentZLevel(entity);
    }

    protected virtual void OnShutdown(Entity<ZPhysicsComponent> entity, ref ComponentShutdown args)
    {
        SleepBody((entity, entity));
    }

    private void OnAnchorStateChanged(Entity<ZPhysicsComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshBody(entity);
    }

    private void OnPhysicsBodyTypeChanged(Entity<ZPhysicsComponent> entity, ref PhysicsBodyTypeChangedEvent args)
    {
        RefreshBody(entity);
    }

    private void OnParentChanged(Entity<ZPhysicsComponent> entity, ref EntParentChangedMessage args)
    {
        RefreshBody(entity);
        SyncCurrentZLevel(entity);

        if (ZPhysicsQuery.TryComp(args.OldParent, out var oldParentPhysics))
        {
            SetZPosition((entity, entity), oldParentPhysics.LocalPosition);
            return;
        }

        if (ZPhysicsQuery.HasComp(Transform(entity).ParentUid))
        {
            SetZPosition((entity, entity), 0);
        }
    }

    private void SyncCurrentZLevel(Entity<ZPhysicsComponent> entity)
    {
        if (!_zMapQuery.TryComp(Transform(entity).MapUid, out var zLevel))
            return;

        if (entity.Comp.CurrentZLevel == zLevel.Depth)
            return;

        entity.Comp.CurrentZLevel = zLevel.Depth;
        DirtyField(entity, entity.Comp, nameof(ZPhysicsComponent.CurrentZLevel));
    }

    [PublicAPI]
    public void WakeBody(Entity<ZPhysicsComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        if (_activeBodies.Contains(entity))
            return;

        entity.Comp.Sleeping = false;
        entity.Comp.SleepTimer = 0f;

        _activeBodies.Add(entity);
    }

    [PublicAPI]
    public void SleepBody(Entity<ZPhysicsComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.Sleeping = true;
        entity.Comp.SleepTimer = 0f;

        _activeBodies.Remove(entity);
    }

    [PublicAPI]
    public void RefreshBody(Entity<ZPhysicsComponent> entity)
    {
        if (TerminatingOrDeleted(entity))
        {
            SleepBody((entity, entity));
            return;
        }

        var transform = Transform(entity);
        var parent = transform.ParentUid;

        var onMap = parent == transform.GridUid || parent == transform.MapUid;

        if (!onMap
            || transform.Anchored
            || _physicsQuery.TryComp(entity, out var physics)
            && physics.BodyType == BodyType.Static)
        {
            SleepBody((entity, entity));
            return;
        }

        WakeBody((entity, entity));
    }
}
