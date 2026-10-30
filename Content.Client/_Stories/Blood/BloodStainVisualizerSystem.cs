using Content.Client.Clothing;
using Content.Shared._Stories.Blood;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.Blood;

public sealed class BloodStainVisualizerSystem : EntitySystem
{
    private const string StainsRsi = "_Stories/Effects/blood_stains.rsi";
    private const string ItemMaskState = "itemblood";
    private const string HandsState = "hands_blood";
    private const string FeetState = "feet_blood";
    private static readonly ProtoId<ShaderPrototype> StainShader = "STBloodStain";

    private const string ItemStainKey = "stories-blood-stain";
    private const string ItemMaskKey = "stories-blood-stain-mask";
    private const string HandsKey = "stories-blood-hands";
    private const string FeetKey = "stories-blood-feet";
    private const string GlovesSlotKey = "gloves";

    [Dependency] private readonly BloodStainSystem _bloodStain = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly List<(SpriteComponent.Layer Layer, int Index)> _sourceLayers = new();
    private readonly List<int> _stainLayers = new();
    private readonly HashSet<int> _ownLayers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodStainComponent, ComponentStartup>(OnStainStartup);
        SubscribeLocalEvent<BloodStainComponent, AfterAutoHandleStateEvent>(OnStainState);
        SubscribeLocalEvent<BloodStainComponent, AppearanceChangeEvent>(OnStainAppearance);
        SubscribeLocalEvent<BloodStainComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: [typeof(ClientClothingSystem)]);

        SubscribeLocalEvent<BloodyComponent, ComponentStartup>(OnBloodyStartup);
        SubscribeLocalEvent<BloodyComponent, AfterAutoHandleStateEvent>(OnBloodyState);
    }

    private void OnStainStartup(Entity<BloodStainComponent> ent, ref ComponentStartup args)
    {
        RefreshItem(ent);
        if (ent.Comp.Color != null)
            _item.VisualsChanged(ent);
    }

    private void OnStainState(Entity<BloodStainComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateItem(ent);
        _item.VisualsChanged(ent);
    }

    private void OnStainAppearance(Entity<BloodStainComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateItem(ent);
    }

    private void OnGetEquipmentVisuals(Entity<BloodStainComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (ent.Comp.Color is not { } color || _bloodStain.GetWornState(ent, args.Slot) is not { } state)
            return;

        args.Layers.Add(($"{args.Slot}-{ItemStainKey}", new PrototypeLayerData
        {
            RsiPath = StainsRsi,
            State = state,
            Color = color,
        }));
    }

    private void UpdateItem(Entity<BloodStainComponent> ent)
    {
        if (ent.Comp.Color is not { } color ||
            !TryComp<SpriteComponent>(ent, out var spriteComp))
        {
            RefreshItem(ent);
            return;
        }

        Entity<SpriteComponent?> sprite = (ent.Owner, spriteComp);
        _stainLayers.Clear();
        _ownLayers.Clear();
        for (var i = 0; _sprite.LayerMapTryGet(sprite, $"{ItemStainKey}-{i}", out var stain, false); i++)
        {
            _stainLayers.Add(stain);
            _ownLayers.Add(stain);
            if (_sprite.LayerMapTryGet(sprite, $"{ItemMaskKey}-{i}", out var mask, false))
                _ownLayers.Add(mask);
        }

        CollectSourceLayers(spriteComp, _ownLayers);
        if (_stainLayers.Count == 0 || _stainLayers.Count != _sourceLayers.Count)
        {
            RefreshItem(ent);
            return;
        }

        for (var i = 0; i < _stainLayers.Count; i++)
        {
            CopySourceLayer(sprite, _sourceLayers[i].Layer, _stainLayers[i]);
            _sprite.LayerSetColor(sprite, _stainLayers[i], color);
        }
    }

    private void RefreshItem(Entity<BloodStainComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (ent.Owner, spriteComp);
        for (var i = 0; ; i++)
        {
            var removedStain = _sprite.RemoveLayer(sprite, $"{ItemStainKey}-{i}", false);
            var removedMask = _sprite.RemoveLayer(sprite, $"{ItemMaskKey}-{i}", false);
            if (!removedStain && !removedMask)
                break;
        }

        if (ent.Comp.Color is not { } color)
            return;

        _ownLayers.Clear();
        CollectSourceLayers(spriteComp, _ownLayers);
        for (var i = _sourceLayers.Count - 1; i >= 0; i--)
        {
            var (layer, layerIndex) = _sourceLayers[i];
            AddStainLayers(sprite, layer, layerIndex, i, color);
        }
    }

    private void CollectSourceLayers(SpriteComponent sprite, HashSet<int> skip)
    {
        _sourceLayers.Clear();
        var index = -1;
        foreach (var candidate in sprite.AllLayers)
        {
            index++;
            if (skip.Contains(index) ||
                candidate is not SpriteComponent.Layer layer ||
                !layer.Visible ||
                layer.Blank ||
                layer.CopyToShaderParameters != null)
            {
                continue;
            }

            _sourceLayers.Add((layer, index));
        }
    }

    private void AddStainLayers(Entity<SpriteComponent?> sprite, SpriteComponent.Layer source, int sourceIndex, int number, Color color)
    {
        var stainKey = $"{ItemStainKey}-{number}";
        var mask = _sprite.AddLayer(sprite, new PrototypeLayerData
        {
            RsiPath = StainsRsi,
            State = ItemMaskState,
            CopyToShaderParameters = new PrototypeCopyToShaderParameters
            {
                LayerKey = stainKey,
                ParameterTexture = "stainMask",
                ParameterUV = "stainMaskUV",
            },
        }, sourceIndex + 1);
        _sprite.LayerMapSet(sprite, $"{ItemMaskKey}-{number}", mask);

        var stain = _sprite.AddLayer(sprite, new PrototypeLayerData
        {
            Shader = StainShader,
            Color = color,
        }, mask + 1);

        CopySourceLayer(sprite, source, stain);
        _sprite.LayerMapSet(sprite, stainKey, stain);
    }

    private void CopySourceLayer(Entity<SpriteComponent?> sprite, SpriteComponent.Layer source, int stain)
    {
        if (source.State.IsValid && source.ActualRsi != null)
            _sprite.LayerSetRsi(sprite, stain, source.ActualRsi, source.State);
        else
            _sprite.LayerSetTexture(sprite, stain, source.Texture);

        _sprite.LayerSetOffset(sprite, stain, source.Offset);
        _sprite.LayerSetScale(sprite, stain, source.Scale);
        _sprite.LayerSetRotation(sprite, stain, source.Rotation);
        _sprite.LayerSetDirOffset(sprite, stain, source.DirOffset);
    }

    private void OnBloodyStartup(Entity<BloodyComponent> ent, ref ComponentStartup args)
    {
        RefreshBody(ent);
    }

    private void OnBloodyState(Entity<BloodyComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshBody(ent);
    }

    private void RefreshBody(Entity<BloodyComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (ent.Owner, spriteComp);
        _sprite.RemoveLayer(sprite, HandsKey, false);
        _sprite.RemoveLayer(sprite, FeetKey, false);

        if (ent.Comp.FeetColor is { } feet &&
            _sprite.LayerMapTryGet(sprite, HumanoidVisualLayers.LHand, out var feetIndex, false))
        {
            AddBodyLayer(sprite, FeetKey, FeetState, feet, feetIndex);
        }

        if (ent.Comp.HandsColor is { } hands &&
            _sprite.LayerMapTryGet(sprite, GlovesSlotKey, out var handsIndex, false))
        {
            AddBodyLayer(sprite, HandsKey, HandsState, hands, handsIndex);
        }
    }

    private void AddBodyLayer(Entity<SpriteComponent?> sprite, string key, string state, Color color, int index)
    {
        var layer = _sprite.AddLayer(sprite, new PrototypeLayerData
        {
            RsiPath = StainsRsi,
            State = state,
            Color = color,
        }, index);
        _sprite.LayerMapSet(sprite, key, layer);
    }
}
