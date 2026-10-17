using System.Globalization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.MilitaryPolice;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class RMCIncidentReport
{
    public const int MaxEntries = 32;

    [DataField]
    public Guid Id = Guid.NewGuid();

    [DataField]
    public EntProtoId<RMCMarineLawCatalogueComponent> LawCatalogue = "RMCMarineLaw";

    /// <summary>The issued sentence is retained independently of subsequent changes to the law prototypes.</summary>
    [DataField]
    public RMCMarineSentence? FinalSentence;

    [DataField]
    public RMCBrigSentence? Detention;

    [DataField]
    public string Suspect = string.Empty;

    [DataField]
    public uint? RecordId;

    [DataField]
    public NetEntity? RecordStation;

    [DataField]
    public string Summary = string.Empty;

    [DataField]
    public List<RMCIncidentCharge> Charges = new();

    [DataField]
    public List<RMCIncidentEntry> Witnesses = new();

    [DataField]
    public List<RMCIncidentEntry> Evidence = new();

    public bool TryCalculateSentence(IPrototypeManager prototypes, IComponentFactory factory,
        out RMCMarineSentence sentence)
    {
        sentence = default;
        if (Charges.Count == 0 || !prototypes.TryIndex(LawCatalogue, out var prototype) ||
            !prototype.TryGetComponent(out RMCMarineLawCatalogueComponent? catalogue, factory))
            return false;

        var ordinary = 0f;
        var extension = 0f;
        var bypassLimit = false;
        foreach (var charge in Charges)
        {
            if (!catalogue.Laws.Contains(charge.Law) || !RMCMarineLaw.TryGet(prototypes, factory, charge.Law, out var law))
                return false;

            var punishment = law;
            if (law.Punishment == RMCMarineLawPunishment.SameAsCrime)
            {
                if (charge.RelatedCrime is not { } related || !catalogue.Laws.Contains(related) ||
                    !RMCMarineLaw.TryGet(prototypes, factory, related, out punishment) ||
                    punishment.Punishment == RMCMarineLawPunishment.SameAsCrime)
                    return false;
            }

            switch (punishment.Punishment)
            {
                case RMCMarineLawPunishment.Brig:
                    if (punishment.BrigTime <= 0 || !float.IsFinite(punishment.BrigTime))
                        return false;
                    if (punishment.ExtendsSentence)
                        extension += punishment.BrigTime;
                    else
                        ordinary += punishment.BrigTime;
                    bypassLimit |= punishment.BypassBrigLimit;
                    break;
                case RMCMarineLawPunishment.NJP:
                    sentence.NJP = true;
                    break;
                case RMCMarineLawPunishment.Capital:
                    sentence.Punishment = RMCMarineLawPunishment.Capital;
                    break;
                case RMCMarineLawPunishment.Permanent:
                    if (sentence.Punishment != RMCMarineLawPunishment.Capital)
                        sentence.Punishment = RMCMarineLawPunishment.Permanent;
                    break;
                default:
                    return false;
            }
        }

        if (sentence.Punishment is RMCMarineLawPunishment.Capital or RMCMarineLawPunishment.Permanent)
            return true;

        sentence.BrigTime = (bypassLimit ? ordinary : Math.Min(catalogue.MaxBrigTime, ordinary)) + extension;
        sentence.Punishment = sentence.BrigTime > 0 ? RMCMarineLawPunishment.Brig : RMCMarineLawPunishment.NJP;
        return true;
    }

    public RMCIncidentReport Clone()
    {
        return new RMCIncidentReport
        {
            Id = Id,
            LawCatalogue = LawCatalogue,
            FinalSentence = FinalSentence,
            Detention = Detention,
            Suspect = Suspect,
            RecordId = RecordId,
            RecordStation = RecordStation,
            Summary = Summary,
            Charges = Charges.ConvertAll(charge => new RMCIncidentCharge(charge.Law, charge.RelatedCrime)),
            Witnesses = Witnesses.ConvertAll(entry => new RMCIncidentEntry(entry.Name, entry.Notes)),
            Evidence = Evidence.ConvertAll(entry => new RMCIncidentEntry(entry.Name, entry.Notes)),
        };
    }
}

[Serializable, NetSerializable, DataDefinition]
public partial struct RMCMarineSentence
{
    [DataField]
    public RMCMarineLawPunishment Punishment;

    [DataField]
    public float BrigTime;

    [DataField]
    public bool NJP;

    public readonly string GetDescription()
    {
        var description = DescribePunishment(Punishment, BrigTime);
        return NJP && Punishment != RMCMarineLawPunishment.NJP
            ? Loc.GetString("rmc-police-sentence-with-njp", ("sentence", description))
            : description;
    }

    public static string DescribePunishment(RMCMarineLawPunishment punishment, float brigTime)
    {
        return punishment == RMCMarineLawPunishment.Brig
            ? Loc.GetString("rmc-police-minutes", ("minutes", brigTime.ToString("0.##", CultureInfo.InvariantCulture)))
            : Loc.GetString($"rmc-police-punishment-{punishment}");
    }

    public static string DescribePunishmentField(RMCMarineLawPunishment punishment, float brigTime)
    {
        return Loc.GetString(punishment == RMCMarineLawPunishment.Brig
                ? "rmc-police-brig-time-value" : "rmc-police-punishment-value",
            ("punishment", DescribePunishment(punishment, brigTime)));
    }
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class RMCIncidentCharge
{
    [DataField(required: true)]
    public EntProtoId<RMCMarineLawComponent> Law;

    [DataField]
    public EntProtoId<RMCMarineLawComponent>? RelatedCrime;

    public RMCIncidentCharge() { }

    public RMCIncidentCharge(EntProtoId<RMCMarineLawComponent> law, EntProtoId<RMCMarineLawComponent>? relatedCrime = null)
    {
        Law = law;
        RelatedCrime = relatedCrime;
    }
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class RMCIncidentEntry
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Notes = string.Empty;

    public RMCIncidentEntry() { }

    public RMCIncidentEntry(string name, string notes = "")
    {
        Name = name;
        Notes = notes;
    }
}
