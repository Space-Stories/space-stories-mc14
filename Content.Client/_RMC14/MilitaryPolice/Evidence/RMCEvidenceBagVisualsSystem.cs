using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.MilitaryPolice.Evidence;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.MilitaryPolice.Evidence;

public sealed class RMCEvidenceBagVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _queuedUpdates = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AppearanceSystem));

        SubscribeLocalEvent<RMCEvidenceBagVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RMCEvidenceBagVisualsComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<RMCEvidenceBagVisualsComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ItemComponent, ComponentStartup>(OnContentsStartup);
        SubscribeLocalEvent<ItemComponent, AppearanceChangeEvent>(OnContentsAppearanceChange);
    }

    public override void FrameUpdate(float frameTime)
    {
        // Contents may acquire additional layers during initialization or appearance updates.
        foreach (var uid in _queuedUpdates)
        {
            if (TryComp<RMCEvidenceBagVisualsComponent>(uid, out var visuals))
                UpdateVisuals((uid, visuals));
        }

        _queuedUpdates.Clear();
    }

    private void OnStartup(Entity<RMCEvidenceBagVisualsComponent> ent, ref ComponentStartup args)
    {
        _queuedUpdates.Add(ent.Owner);
    }

    private void OnInserted(Entity<RMCEvidenceBagVisualsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (TryComp<RMCEvidenceBagComponent>(ent, out var bag) && args.Container.ID == bag.ContainerId)
            _queuedUpdates.Add(ent.Owner);
    }

    private void OnRemoved(Entity<RMCEvidenceBagVisualsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<RMCEvidenceBagComponent>(ent, out var bag) && args.Container.ID == bag.ContainerId)
            _queuedUpdates.Add(ent.Owner);
    }

    private void OnContentsStartup(Entity<ItemComponent> ent, ref ComponentStartup args)
    {
        QueueContentsUpdate(ent.Owner);
    }

    private void OnContentsAppearanceChange(Entity<ItemComponent> ent, ref AppearanceChangeEvent args)
    {
        QueueContentsUpdate(ent.Owner);
    }

    private void QueueContentsUpdate(EntityUid uid)
    {
        if (_container.TryGetContainingContainer(uid, out var container) &&
            HasComp<RMCEvidenceBagVisualsComponent>(container.Owner))
        {
            _queuedUpdates.Add(container.Owner);
        }
    }

    private void UpdateVisuals(Entity<RMCEvidenceBagVisualsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !sprite.Initialized ||
            !TryComp<RMCEvidenceBagComponent>(ent, out var bag) || bag.Container is not { } container)
        {
            return;
        }

        var bagSprite = (ent.Owner, sprite);
        for (var i = sprite.AllLayers.Count() - 1; i >= 0; i--)
            _sprite.RemoveLayer(bagSprite, i);

        if (container.ContainedEntity is not { } evidence)
        {
            _sprite.AddLayer(bagSprite, ent.Comp.EmptySprite);
            return;
        }

        AddFullSprite(bagSprite, ent.Comp);
        var firstContentsLayer = sprite.AllLayers.Count();

        // Copy visible layers without exposing or changing the contained entity's sprite.
        if (TryComp<SpriteComponent>(evidence, out var contents))
        {
            for (var i = 0; _sprite.TryGetLayer((evidence, contents), i, out var layer, logMissing: false); i++)
            {
                if (!layer.Visible || layer.Blank)
                    continue;

                var index = layer.Texture != null
                    ? _sprite.AddTextureLayer(bagSprite, layer.Texture)
                    : _sprite.AddRsiLayer(bagSprite, layer.State, layer.RSI ?? contents.BaseRSI);
                _sprite.LayerSetColor(bagSprite, index, layer.Color * contents.Color);
                _sprite.LayerSetScale(bagSprite, index, layer.Scale * contents.Scale);
                _sprite.LayerSetOffset(bagSprite, index, contents.Rotation.RotateVec(layer.Offset * contents.Scale));
                _sprite.LayerSetRotation(bagSprite, index, layer.Rotation + contents.Rotation);
            }
        }

        FitContents(bagSprite, firstContentsLayer, ent.Comp.ContentsBounds);
        var overlay = AddFullSprite(bagSprite, ent.Comp);
        _sprite.LayerSetColor(bagSprite, overlay, ent.Comp.OverlayColor);
    }

    private int AddFullSprite(Entity<SpriteComponent> sprite, RMCEvidenceBagVisualsComponent visuals)
    {
        var index = _sprite.AddLayer(sprite.AsNullable(), visuals.FullSprite);
        _sprite.LayerSetScale(sprite.AsNullable(), index, visuals.FullSpriteScale);
        _sprite.LayerSetOffset(sprite.AsNullable(), index, visuals.FullSpriteOffset);
        return index;
    }

    private void FitContents(Entity<SpriteComponent> sprite, int firstLayer, Box2 targetBounds)
    {
        Box2? contentsBounds = null;
        for (var i = firstLayer; _sprite.TryGetLayer(sprite.AsNullable(), i, out var layer, logMissing: false); i++)
        {
            var bounds = _sprite.GetLocalBounds(layer);
            contentsBounds = contentsBounds?.Union(bounds) ?? bounds;
        }

        if (contentsBounds is not { } sourceBounds || sourceBounds.Width <= 0 || sourceBounds.Height <= 0)
            return;

        var scale = MathF.Min(1, MathF.Min(targetBounds.Width / sourceBounds.Width,
            targetBounds.Height / sourceBounds.Height));

        // Fit all layers together so their proportions and relative positions remain intact.
        for (var i = firstLayer; _sprite.TryGetLayer(sprite.AsNullable(), i, out var layer, logMissing: false); i++)
        {
            _sprite.LayerSetScale(layer, layer.Scale * scale);
            _sprite.LayerSetOffset(layer, targetBounds.Center + (layer.Offset - sourceBounds.Center) * scale);
        }
    }
}
