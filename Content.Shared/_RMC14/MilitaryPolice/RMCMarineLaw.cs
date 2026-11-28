using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.MilitaryPolice;

/// <summary>
/// Read-only view of an offence's localized entity metadata and prototype sanction.
/// </summary>
public readonly record struct RMCMarineLaw(EntityPrototype Prototype, RMCMarineLawComponent Definition)
{
    public string ID => Prototype.ID;
    public string Name => Prototype.Name;
    public string Description => Prototype.Description;
    public RMCMarineLawCategory Category => Definition.Category;
    public RMCMarineLawPunishment Punishment => Definition.Punishment;
    public float BrigTime => Definition.BrigTime;
    public bool ExtendsSentence => Definition.ExtendsSentence;
    public bool BypassBrigLimit => Definition.BypassBrigLimit;
    public LocId? SpecialPunishment => Definition.SpecialPunishment;
    public LocId? Conditions => Definition.Conditions;

    public static bool TryGet(IPrototypeManager prototypes, IComponentFactory factory,
        EntProtoId<RMCMarineLawComponent> id, out RMCMarineLaw law)
    {
        law = default;
        if (!prototypes.TryIndex(id, out var prototype) ||
            !prototype.TryGetComponent(out RMCMarineLawComponent? definition, factory))
            return false;
        law = new RMCMarineLaw(prototype, definition);
        return true;
    }

    public static RMCMarineLaw Get(IPrototypeManager prototypes, IComponentFactory factory,
        EntProtoId<RMCMarineLawComponent> id)
    {
        return TryGet(prototypes, factory, id, out var law)
            ? law : throw new ArgumentException($"Invalid marine law entity prototype: {id}", nameof(id));
    }

    public static IEnumerable<RMCMarineLaw> Enumerate(IPrototypeManager prototypes, IComponentFactory factory)
    {
        foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.TryGetComponent(out RMCMarineLawComponent? definition, factory))
                yield return new RMCMarineLaw(prototype, definition);
        }
    }
}
