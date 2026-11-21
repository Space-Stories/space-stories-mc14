using System.Linq;
using System.Text;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.MilitaryPolice;

public sealed partial class RMCPoliceSystem
{
    private EntityUid? Print(Entity<RMCPoliceConsoleComponent> ent, EntityUid actor,
        EntProtoId prototype, string name, string content)
    {
        if (_timing.CurTime < ent.Comp.NextPrint)
            return null;

        ent.Comp.NextPrint = _timing.CurTime + TimeSpan.FromSeconds(1.5);
        var paper = Spawn(prototype, Transform(ent).Coordinates);
        Comp<PaperComponent>(paper).EditingDisabled = true;
        _paper.SetContent(paper, content);
        var metadata = MetaData(paper);
        EntityManager.System<MetaDataSystem>().SetEntityName(paper,
            Loc.GetString(ent.Comp.Sentencing ? "rmc-police-incident-print-title" : "rmc-police-print-title",
                ("name", name)), metadata);
        _audio.PlayPvs(ent.Comp.PrintSound, ent);
        LogChange(actor, ent, $"printed {name}");
        Refresh(ent);
        return paper;
    }

    private string FormatRecord(uint id, GeneralStationRecord general, RMCPoliceRecord record)
    {
        var content = DocumentHeader("rmc-police-document-record-title", "MP-R01");
        content.AppendLine(Loc.GetString("rmc-police-record-print",
            ("record", (ulong) id + 1),
            ("name", EscapeDocumentText(general.Name)), ("id", EscapeDocumentText(general.Fingerprint)),
            ("rank", EscapeDocumentText(general.JobTitle)), ("age", general.Age),
            ("squad", EscapeDocumentText(general.Squad)),
            ("gender", Loc.GetString($"rmc-police-gender-{general.Gender}")),
            ("physical", Loc.GetString($"rmc-police-physical-{record.PhysicalStatus}")),
            ("mental", Loc.GetString($"rmc-police-mental-{record.MentalStatus}")),
            ("status", Loc.GetString($"rmc-police-status-{record.Status}")),
            ("crimes", EscapeDocumentText(record.MajorCrimes))));
        DocumentSection(content, "rmc-police-comments");
        if (record.Comments.Count == 0)
            content.AppendLine(Loc.GetString("rmc-police-none"));
        foreach (var comment in record.Comments)
        {
            var text = comment.DeletedBy == null ? comment.Text :
                Loc.GetString("rmc-police-comment-deleted", ("author", comment.DeletedBy),
                    ("time", comment.DeletedAt?.ToString(@"hh\:mm\:ss") ?? ""));
            content.AppendLine().AppendLine(Loc.GetString("rmc-police-document-comment",
                ("author", EscapeDocumentText(comment.Author)),
                ("time", comment.CreatedAt.ToString(@"hh\:mm\:ss")), ("text", EscapeDocumentText(text))));
        }
        DocumentSection(content, "rmc-police-incidents");
        if (record.Incidents.Count == 0)
            content.AppendLine(Loc.GetString("rmc-police-none"));
        foreach (var incident in record.Incidents)
            content.AppendLine().AppendLine(FormatIncident(incident, standalone: false));
        content.AppendLine(Loc.GetString("rmc-police-document-footer"));
        return content.ToString();
    }

    private string FormatIncident(RMCIncidentReport report, bool standalone = true)
    {
        report.TryCalculateSentence(_prototypes, EntityManager.ComponentFactory, out var calculated);
        var time = (report.FinalSentence ?? calculated).GetDescription();
        var content = standalone
            ? DocumentHeader("rmc-police-incident-report", "MP-I01")
            : new StringBuilder();
        content.AppendLine(Loc.GetString("rmc-police-incident-print",
            ("report", report.Id.ToString("N").ToUpperInvariant()),
            ("record", report.RecordId is { } id ? ((ulong) id + 1).ToString() : Loc.GetString("rmc-police-none")),
            ("name", EscapeDocumentText(report.Suspect)), ("summary", EscapeDocumentText(report.Summary))));
        DocumentSection(content, "rmc-police-incident-charges");
        var number = 0;
        foreach (var charge in report.Charges)
        {
            if (!RMCMarineLaw.TryGet(_prototypes, EntityManager.ComponentFactory, charge.Law, out var law))
                continue;
            content.AppendLine().AppendLine(Loc.GetString("rmc-police-document-entry",
                ("number", ++number), ("name", EscapeDocumentText(law.Name))));
            content.AppendLine(EscapeDocumentText(law.Description));
            if (charge.RelatedCrime is { } related && RMCMarineLaw.TryGet(_prototypes, EntityManager.ComponentFactory, related, out var relatedLaw))
                content.AppendLine(Loc.GetString("rmc-police-related-crime-value", ("crime", EscapeDocumentText(relatedLaw.Name))));
            if (law.SpecialPunishment is { } punishment)
                content.AppendLine(EscapeDocumentText(Loc.GetString(punishment)));
            if (law.Conditions is { } conditions)
                content.AppendLine(EscapeDocumentText(Loc.GetString(conditions)));
        }
        DocumentEntries(content, "rmc-police-witnesses", report.Witnesses);
        DocumentEntries(content, "rmc-police-evidence", report.Evidence);
        DocumentSection(content, "rmc-police-document-sentence-title");
        content.AppendLine(Loc.GetString("rmc-police-document-sentence", ("sentence", EscapeDocumentText(time))));
        if (report.Detention is { } detention)
            content.AppendLine(EscapeDocumentText(detention.GetDescription()));
        if (standalone)
            content.AppendLine(Loc.GetString("rmc-police-document-footer"));
        return content.ToString();
    }

    private string FormatFingerprints(List<RMCIncidentEntry> entries)
    {
        var content = DocumentHeader("rmc-police-fingerprints", "MP-F01");
        content.AppendLine(Loc.GetString("rmc-police-document-fingerprint-count", ("count", entries.Count)));
        DocumentEntries(content, "rmc-police-document-fingerprint-results", entries);
        content.AppendLine(Loc.GetString("rmc-police-document-footer"));
        return content.ToString();
    }

    private StringBuilder DocumentHeader(LocId title, string form)
    {
        return new StringBuilder(Loc.GetString("rmc-police-document-header",
            ("title", Loc.GetString(title)), ("form", form))).AppendLine();
    }

    private void DocumentSection(StringBuilder content, LocId title)
    {
        content.AppendLine().AppendLine(Loc.GetString("rmc-police-document-section", ("title", Loc.GetString(title))));
    }

    private void DocumentEntries(StringBuilder content, LocId title, List<RMCIncidentEntry> entries)
    {
        DocumentSection(content, title);
        if (entries.Count == 0)
            content.AppendLine(Loc.GetString("rmc-police-none"));
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            content.AppendLine().AppendLine(Loc.GetString("rmc-police-document-entry",
                ("number", i + 1), ("name", EscapeDocumentText(entry.Name))));
            if (!string.IsNullOrWhiteSpace(entry.Notes))
                content.AppendLine(EscapeDocumentText(entry.Notes));
        }
    }

    // Escape individual values so names and notes cannot inject interactive tags into an official form.
    private string EscapeDocumentText(string? text)
        => FormattedMessage.EscapeText(string.IsNullOrWhiteSpace(text) ? Loc.GetString("rmc-police-none") : text);

    public void SynchronizeIncidentReport(Entity<RMCIncidentReportPaperComponent> paper)
    {
        if (paper.Comp.Report is not { } report)
            return;

        _paper.SetContent(paper, FormatIncident(report));
        if (report.RecordId is not { } id || report.RecordStation is not { } station ||
            !TryGetEntity(station, out var stationUid))
            return;

        var key = new StationRecordKey(id, stationUid.Value);
        if (!_records.TryGetRecord<RMCPoliceRecord>(key, out var record))
            return;

        var incident = record.Incidents.FirstOrDefault(entry => entry.Id == report.Id);
        if (incident == null)
            return;
        incident.Detention = report.Detention;
        _records.Synchronize(key);
    }
}
