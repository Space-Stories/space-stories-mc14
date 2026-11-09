using System.Linq;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Storage;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.Containers;
using Content.Shared.Item;
using Content.Shared.Prototypes;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

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

        if (TryGetMappedContainerDisplayIcon(prototype, out icon))
            return true;

        prototype.TryGetComponent<CMItemSlotsComponent>(out var slots, factory);
        prototype.TryGetComponent<BulletBoxComponent>(out var bullets, factory);
        prototype.TryGetComponent<CMStorageVisualizerComponent>(out var storage, factory);
        var storageFilled = storage != null && HasStartingContents(prototype);
        if (slots == null && bullets == null && !storageFilled)
            return false;

        // Client preview entities have no server-filled containers. Read the declared starting appearance instead.
        var layers = new Dictionary<int, PrototypeLayerData>();
        if (slots != null)
        {
            var total = Math.Max(0, slots.Count ?? 0);
            var filled = slots.StartingItem != null ? total : Math.Min(total, slots.StartingItems?.Count ?? 0);
            AddContainerVisual(prototype, sprite, CMItemSlotsLayers.Fill,
                SharedCMInventorySystem.GetFillVisuals(filled, total), layers);
            if (_sprite.LayerMapTryGet((EntityUid.Invalid, sprite), CMItemSlotsLayers.Fill, out var fill, false) &&
                !layers.ContainsKey(fill))
                layers[fill] = new PrototypeLayerData { Visible = filled > 0 };
        }
        if (bullets != null)
            AddContainerVisual(prototype, sprite, BulletBoxLayers.Fill,
                BulletBoxSystem.GetFillVisuals(bullets.Amount, bullets.Max), layers);
        if (storage != null && storageFilled)
        {
            SetContainerLayer(sprite, storage.StorageOpen, false, layers);
            SetContainerLayer(sprite, storage.StorageClosed, true, layers);
            SetContainerLayer(sprite, storage.StorageEmpty, false, layers);
        }

        icon = GetContainerDisplayIcon(sprite, layers);
        return true;
    }

    private DisplayIcon GetContainerDisplayIcon(SpriteComponent sprite, Dictionary<int, PrototypeLayerData> layers)
    {
        var textures = new List<Texture>();
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            layers.TryGetValue(index++, out var replacement);
            if (!(replacement?.Visible ?? layer.Visible))
                continue;
            if (layer.Texture != null)
            {
                textures.Add(layer.Texture);
                continue;
            }
            var rsi = layer.Rsi ?? sprite.BaseRSI;
            if (rsi != null && rsi.TryGetState(replacement?.State ?? layer.RsiState.ToString(), out var state))
                textures.Add(state.Frame0);
        }
        if (textures.Count == 0)
            textures.Add(_sprite.GetFallbackTexture());
        return new DisplayIcon(textures, sprite.AllLayers.FirstOrDefault()?.Color ?? Color.White);
    }

    private void AddContainerVisual(
        EntityPrototype prototype, SpriteComponent sprite, Enum key, Enum value,
        Dictionary<int, PrototypeLayerData> layers)
    {
        if (!prototype.TryGetComponent<GenericVisualizerComponent>(out var visualizer, _entities.ComponentFactory))
            return;
        IReadOnlyDictionary<Enum, Dictionary<string, Dictionary<string, PrototypeLayerData>>> visuals =
            visualizer.Visuals;
        if (!visuals.TryGetValue(key, out var mapping))
            return;
        foreach (var (layerKey, states) in mapping)
        {
            if (!states.TryGetValue(value.ToString(), out var data))
                continue;
            var found = _reflection.TryParseEnumReference(layerKey, out var layerEnum)
                ? _sprite.LayerMapTryGet((EntityUid.Invalid, sprite), layerEnum, out var index, false)
                : _sprite.LayerMapTryGet((EntityUid.Invalid, sprite), layerKey, out index, false);
            if (found)
                layers[index] = data;
        }
    }

    private void SetContainerLayer(
        SpriteComponent sprite, string? key, bool visible, Dictionary<int, PrototypeLayerData> layers)
    {
        if (key != null && _sprite.LayerMapTryGet((EntityUid.Invalid, sprite), key, out var index, false))
            layers[index] = new PrototypeLayerData { Visible = visible };
    }

    private bool TryGetMappedContainerDisplayIcon(EntityPrototype prototype, out DisplayIcon icon)
    {
        icon = default;
        var factory = _entities.ComponentFactory;
        if (!prototype.HasComponent<ItemMapperComponent>(factory) ||
            !prototype.HasComponent<AppearanceComponent>(factory) ||
            !prototype.HasComponent<StorageComponent>(factory) ||
            !prototype.TryGetComponent<StorageFillComponent>(out var fill, factory) ||
            fill.Contents.Count == 0 || fill.Contents.Any(item => item.PrototypeId == null ||
                item.SpawnProbability != 1 || item.GroupId != null || item.MaxAmount > item.Amount))
            return false;

        // ItemMapper builds its layers from the actual item types rather than a fill level.
        var preview = _entities.CreateEntityUninitialized(prototype.ID, MapCoordinates.Nullspace);
        var items = new List<EntityUid>();
        try
        {
            // UI refreshes can run while server state is being applied; preview appearance must still update.
            _entities.GetComponent<AppearanceComponent>(preview).NetSyncEnabled = false;
            _entities.InitializeAndStartEntity(preview);
            var storage = _entities.System<SharedStorageSystem>();
            var container = _entities.GetComponent<StorageComponent>(preview);
            foreach (var item in fill.Contents)
            {
                for (var i = 0; i < item.Amount; i++)
                {
                    var uid = _entities.SpawnEntity(item.PrototypeId, MapCoordinates.Nullspace);
                    items.Add(uid);
                    var ev = new CMStorageItemFillEvent((uid, _entities.GetComponent<ItemComponent>(uid)), container);
                    _entities.EventBus.RaiseLocalEvent(preview, ref ev);
                    storage.Insert(preview, uid, out _, playSound: false);
                }
            }
            var sprite = _entities.GetComponent<SpriteComponent>(preview);
            _entities.System<AppearanceSystem>().OnChangeData(preview, sprite);
            icon = GetContainerDisplayIcon(sprite, new Dictionary<int, PrototypeLayerData>());
            return true;
        }
        finally
        {
            _entities.DeleteEntity(preview);
            foreach (var uid in items)
            {
                if (_entities.EntityExists(uid))
                    _entities.DeleteEntity(uid);
            }
        }
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
