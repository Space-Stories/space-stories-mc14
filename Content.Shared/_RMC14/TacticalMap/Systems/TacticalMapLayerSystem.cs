using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.TacticalMap;

public sealed class TacticalMapLayerSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly Dictionary<EntProtoId<TacticalMapLayerComponent>, TacticalMapLayerComponent> _definitions = new();
    private bool _loaded;

    public IReadOnlyDictionary<EntProtoId<TacticalMapLayerComponent>, TacticalMapLayerComponent> Definitions
    {
        get
        {
            if (!_loaded)
                RebuildDefinitions();

            return _definitions;
        }
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    public bool TryGet(EntProtoId<TacticalMapLayerComponent> id,
        [NotNullWhen(true)] out TacticalMapLayerComponent? layer)
    {
        return Definitions.TryGetValue(id, out layer);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            RebuildDefinitions();
    }

    private void RebuildDefinitions()
    {
        _loaded = true;
        _definitions.Clear();
        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.TryGetComponent(out TacticalMapLayerComponent? layer, _factory))
                _definitions.Add(new EntProtoId<TacticalMapLayerComponent>(prototype.ID), layer);
        }
    }
}
