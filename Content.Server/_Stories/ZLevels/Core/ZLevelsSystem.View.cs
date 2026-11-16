using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Analyzers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.ZLevels.Core;

public sealed partial class ZLevelsSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly EntProtoId _zEyeProto = "STZLevelEye";

    private readonly TimeSpan _zLevelViewerUpdateRate = TimeSpan.FromSeconds(0.2f);
    private TimeSpan _nextZLevelViewerUpdate = TimeSpan.Zero;

    private void InitView()
    {
        SubscribeLocalEvent<ZMapNetworkComponent, ZLevelMapNetworkUpdatedEvent>(OnMapNetworkUpdated);
        SubscribeLocalEvent<ZLevelViewerComponent, MapInitEvent>(OnViewerInit);
        SubscribeLocalEvent<ZLevelViewerComponent, ComponentRemove>(OnCompRemove);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ZLevelViewerComponent, MapUidChangedEvent>(OnViewerMapUidChanged);
    }

    protected override void OnViewerMove(Entity<ZLevelViewerComponent> ent, ref MoveEvent args)
    {
        base.OnViewerMove(ent, ref args);

        if (TerminatingOrDeleted(ent) || !_transform.IsValid(args.NewPosition))
            return;

        var worldPosition = _transform.ToMapCoordinates(args.NewPosition).Position;
        foreach (var eye in ent.Comp.Eyes)
        {
            if (TerminatingOrDeleted(eye))
                continue;

            _transform.SetWorldPosition(eye, worldPosition);
        }
    }

    /// <summary>
    /// A viewer's eye stack is built from the maps surrounding it, so it goes stale the moment
    /// the network gains or loses a level. Rebuild it for everyone inside that network.
    /// </summary>
    private void OnMapNetworkUpdated(Entity<ZMapNetworkComponent> ent, ref ZLevelMapNetworkUpdatedEvent args)
    {
        var query = EntityQueryEnumerator<ZLevelViewerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var xform))
        {
            if (xform.MapUid is not { } mapUid ||
                !TryComp<ZMapComponent>(mapUid, out var zMap) ||
                zMap.NetworkUid != ent.Owner)
                continue;

            UpdateViewer((uid, viewer));
        }
    }

    private void UpdateView(float frameTime)
    {
        if (_timing.CurTime < _nextZLevelViewerUpdate)
            return;
        _nextZLevelViewerUpdate = _timing.CurTime + _zLevelViewerUpdateRate;

        var query = EntityQueryEnumerator<ZLevelViewerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var xform))
        {
            var worldPosition = _transform.GetWorldPosition(xform);

            foreach (var eye in viewer.Eyes)
            {
                if (TerminatingOrDeleted(eye))
                    continue;

                _transform.SetWorldPosition(eye, worldPosition);
            }
        }
    }

    private void OnViewerInit(Entity<ZLevelViewerComponent> ent, ref MapInitEvent args)
    {
        _meta.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
        UpdateLookUpAlert(ent);
    }

    private void OnCompRemove(Entity<ZLevelViewerComponent> ent, ref ComponentRemove args)
    {
        _alerts.ClearAlert(ent, LookUpAlert);
        if (ent.Comp.ActionEntity != null)
            _actions.RemoveAction(ent.Comp.ActionEntity);
        _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);

        foreach (var eye in ent.Comp.Eyes)
        {
            QueueDel(eye);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var viewer = EnsureComp<ZLevelViewerComponent>(ev.Entity);
        UpdateViewer((ev.Entity, viewer));
        UpdateLookUpAlert((ev.Entity, viewer));
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        _alerts.ClearAlert(ev.Entity, LookUpAlert);
        RemComp<ZLevelViewerComponent>(ev.Entity);
    }

    private void OnViewerMapUidChanged(Entity<ZLevelViewerComponent> ent, ref MapUidChangedEvent args)
    {
        UpdateViewer(ent);
        UpdateLookUpAlert(ent);
    }

    private void UpdateViewer(Entity<ZLevelViewerComponent> ent)
    {
        var eyes = ent.Comp.Eyes;
        foreach (var eye in ent.Comp.Eyes)
        {
            if (!TerminatingOrDeleted(eye))
                QueueDel(eye);
        }
        eyes.Clear();

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var xform = Transform(ent);
        var map = xform.MapUid;

        if (map is null)
            return;

        var globalPos = _transform.GetWorldPosition(xform);

        for (var i = 1; i <= MaxZLevelsBelowRendering; i++)
        {
            if (!TryMapOffset(map.Value, -i, out var mapUidBelow))
                break;

            var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(mapUidBelow, globalPos));

            Transform(newEye).GridTraversal = false;
            _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
            eyes.Add(newEye);
        }

        // We constantly load the upper z-levels for the client so that you can quickly look up and climb stairs without PVS lag.
        for (var i = 1; i <= MaxZLevelsAboveRendering; i++)
        {
            if (!TryMapOffset(map.Value, i, out var mapUidAbove))
                break;

            var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(mapUidAbove, globalPos));

            Transform(newEye).GridTraversal = false;
            _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
            eyes.Add(newEye);
        }
    }
}
