using System.Linq;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Storage;
using Content.Shared.Containers;
using Content.Shared.Prototypes;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionsBui
{
    private bool TryGetContainerDisplayIcon(EntityPrototype prototype, out DisplayIcon icon)
    {
        icon = default;
        var factory = _entities.ComponentFactory;
        if (prototype.HasComponent<IconComponent>(factory) ||
            prototype.HasComponent<CMHolsterComponent>(factory) ||
            !prototype.TryGetComponent<SpriteComponent>(out var sprite, factory))
            return false;

        if (TryGetMappedContainerDisplayIcon(prototype, sprite, out icon))
            return true;

        if (prototype.TryGetComponent<CMItemSlotsComponent>(out var slots, factory) &&
            (slots.Count == 0 || slots.StartingItem == null && slots.StartingItems is not { Count: > 0 }))
        {
            // Network state application suppresses appearance updates for the secondary shell layer.
            var emptyLayers = new Dictionary<int, bool>();
            if (_sprite.LayerMapTryGet((EntityUid.Invalid, sprite), CMItemSlotsLayers.Fill, out var fill, false))
                emptyLayers[fill] = false;
            if (_sprite.LayerMapTryGet((EntityUid.Invalid, sprite), CMItemSlotsLayers2.Fill, out var second, false))
                emptyLayers[second] = false;
            icon = GetContainerDisplayIcon(sprite, emptyLayers);
            return true;
        }

        if (!prototype.TryGetComponent<CMStorageVisualizerComponent>(out var storage, factory) ||
            !HasStartingContents(prototype))
            return false;

        // StorageFill runs on the server, so the ordinary prototype preview sees these boxes as empty.
        var layers = new Dictionary<int, bool>();
        SetContainerLayer(sprite, storage.StorageOpen, false, layers);
        SetContainerLayer(sprite, storage.StorageClosed, true, layers);
        SetContainerLayer(sprite, storage.StorageEmpty, false, layers);
        icon = GetContainerDisplayIcon(sprite, layers);
        return true;
    }

    private DisplayIcon GetContainerDisplayIcon(SpriteComponent sprite, IReadOnlyDictionary<int, bool> layers)
    {
        var textures = new List<Texture>();
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (!layers.GetValueOrDefault(index++, layer.Visible))
                continue;
            if (layer.Texture != null)
            {
                textures.Add(layer.Texture);
                continue;
            }
            var rsi = layer.Rsi ?? sprite.BaseRSI;
            if (rsi != null && rsi.TryGetState(layer.RsiState.ToString(), out var state))
                textures.Add(state.Frame0);
        }
        if (textures.Count == 0)
            textures.Add(_sprite.GetFallbackTexture());
        return new DisplayIcon(textures, sprite.AllLayers.FirstOrDefault()?.Color ?? Color.White);
    }

    private void SetContainerLayer(
        SpriteComponent sprite, string? key, bool visible, Dictionary<int, bool> layers)
    {
        if (key != null && _sprite.LayerMapTryGet((EntityUid.Invalid, sprite), key, out var index, false))
            layers[index] = visible;
    }

    private bool TryGetMappedContainerDisplayIcon(
        EntityPrototype prototype, SpriteComponent sprite, out DisplayIcon icon)
    {
        icon = default;
        var factory = _entities.ComponentFactory;
        if (!prototype.TryGetComponent<ItemMapperComponent>(out var mapper, factory) ||
            !prototype.TryGetComponent<StorageFillComponent>(out var fill, factory) ||
            fill.Contents.Count == 0 || fill.Contents.Any(item => item.PrototypeId == null ||
                item.SpawnProbability != 1 || item.GroupId != null || item.MaxAmount > item.Amount) ||
            (mapper.RSIPath ?? sprite.BaseRSI?.Path) is not { } path)
            return false;

        // Static tag-based mappings can be resolved from prototypes without spawning the box or its contents.
        IReadOnlyDictionary<string, SharedMapLayerData> mappings = mapper.MapLayers;
        if (mappings.Values.Any(layer => layer.MinCount < 1 || layer.Whitelist is { } whitelist &&
                (whitelist.Tags == null || whitelist.Components != null || whitelist.Sizes != null ||
                 whitelist.Skills != null || whitelist.MinMobSize != null)))
            return false;

        var textures = _sprite.GetPrototypeTextures(prototype).Select(texture => texture.Default).ToList();
        var tags = _entities.System<TagSystem>();
        foreach (var (name, mapping) in mappings)
        {
            long count = 0;
            foreach (var item in fill.Contents)
            {
                var child = _prototypes.Index(item.PrototypeId!.Value);
                if (mapping.Whitelist is { } whitelist)
                {
                    if (!child.TryGetComponent<TagComponent>(out var childTags, factory))
                        continue;
                    var matches = whitelist.RequireAll
                        ? tags.HasAllTags(childTags, whitelist.Tags!)
                        : tags.HasAnyTag(childTags, whitelist.Tags!);
                    if (!matches)
                        continue;
                }
                count += Math.Max(0, item.Amount);
            }
            if (count >= mapping.MinCount && count <= mapping.MaxCount)
                textures.Add(_sprite.GetState(new SpriteSpecifier.Rsi(path, name)).Frame0);
        }
        icon = new DisplayIcon(textures, sprite.AllLayers.FirstOrDefault()?.Color ?? Color.White);
        return true;
    }

    private bool HasStartingContents(EntityPrototype prototype)
    {
        var factory = _entities.ComponentFactory;
        return prototype.TryGetComponent<StorageFillComponent>(out var storage, factory) &&
                   storage.Contents.Any(item => item.PrototypeId != null && item.SpawnProbability > 0 &&
                                                Math.Max(item.Amount, item.MaxAmount) > 0) ||
               prototype.TryGetComponent<ContainerFillComponent>(out var containers, factory) &&
                   containers.Containers.Values.Any(items => items.Count > 0) ||
               prototype.TryGetComponent<EntityTableContainerFillComponent>(out var tables, factory) &&
                   tables.Containers.Count > 0;
    }
}
