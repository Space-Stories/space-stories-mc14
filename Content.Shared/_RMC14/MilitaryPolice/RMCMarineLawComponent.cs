using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.MilitaryPolice;

[Serializable, NetSerializable]
public enum RMCMarineLawCategory : byte
{
    Variable,
    Minor,
    Major,
    Capital,
    Optional,
    Precautionary,
    Civil, // Stories-FederalLaw
}

[Serializable, NetSerializable]
public enum RMCMarineLawPunishment : byte
{
    None,
    Brig,
    NJP,
    Permanent,
    Capital,
    SameAsCrime,
}

/// <summary>
/// An offence and its sanction from a marine law guidebook. Variable offences have separate severity variants.
/// </summary>
[RegisterComponent]
public sealed partial class RMCMarineLawComponent : Component
{
    [DataField(required: true)]
    public RMCMarineLawCategory Category;

    [DataField(required: true)]
    public RMCMarineLawPunishment Punishment;

    [DataField]
    public float BrigTime;

    /// <summary>Additional time for offences committed in confinement, applied after the ordinary brig limit.</summary>
    [DataField]
    public bool ExtendsSentence;

    /// <summary>Whether this offence permits exceeding the catalogue's ordinary brig limit.</summary>
    [DataField]
    public bool BypassBrigLimit;

    [DataField]
    public LocId? SpecialPunishment;

    [DataField]
    public LocId? Conditions;
}
