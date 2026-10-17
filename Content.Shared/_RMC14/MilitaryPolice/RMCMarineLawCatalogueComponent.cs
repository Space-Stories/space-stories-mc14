using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.MilitaryPolice;

/// <summary>
/// The offences available to the police consoles and the guidebook governing their use.
/// </summary>
[RegisterComponent]
public sealed partial class RMCMarineLawCatalogueComponent : Component
{
    [DataField(required: true)]
    public ProtoId<GuideEntryPrototype> Guide;

    [DataField(required: true)]
    public List<EntProtoId<RMCMarineLawComponent>> Laws = new();

    [DataField]
    public float MaxBrigTime = 30;

    [DataField]
    public Dictionary<RMCMarineLawCategory, LocId> SectionNotes = new();

    [DataField(required: true)]
    public LocId SentencingNotes;
}
