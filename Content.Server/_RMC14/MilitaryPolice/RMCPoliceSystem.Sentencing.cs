using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared.Access.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Paper;
using Content.Shared.StationRecords;

namespace Content.Server._RMC14.MilitaryPolice;

public sealed partial class RMCPoliceSystem
{
    private void OnSentencingMessage(Entity<RMCPoliceConsoleComponent> ent, ref RMCSentencingMsg args)
    {
        if (!ent.Comp.Sentencing || !CanUse(ent, args.Actor) || args.Text.Length > MaxTextLength)
            return;

        if (args.Revision != ent.Comp.Revision)
        {
            Error(args.Actor, "rmc-police-report-changed");
            Refresh(ent);
            return;
        }

        if (args.Action == RMCSentencingAction.New)
        {
            if (ent.Comp.Report == null && _prototypes.TryIndex(_lawCatalogue, out var prototype) &&
                prototype.TryGetComponent(out RMCMarineLawCatalogueComponent? _, EntityManager.ComponentFactory))
                ent.Comp.Report = new RMCIncidentReport { LawCatalogue = _lawCatalogue };
        }
        else if (ent.Comp.Report is { } report)
        {
            switch (args.Action)
            {
                case RMCSentencingAction.Scrap:
                    ent.Comp.Report = null;
                    break;
                case RMCSentencingAction.Suspect:
                    if (TryRegistrationSubject(ent, args.Actor, out var person) &&
                        _rmcRecords.TryGetRecordKey(person, GetStation(ent), out var personKey) &&
                        _records.TryGetRecord<GeneralStationRecord>(personKey, out var personRecord))
                    {
                        report.Suspect = personRecord.Name;
                        report.RecordId = personKey.Id;
                        report.RecordStation = GetNetEntity(personKey.OriginStation);
                    }
                    else if (TrySuspectId(args.Actor, out var card, out var key))
                    {
                        report.Suspect = card.FullName ?? string.Empty;
                        report.RecordId = key?.OriginStation == GetStation(ent) ? key?.Id : null;
                        report.RecordStation = key is { } recordKey ? GetNetEntity(recordKey.OriginStation) : null;
                    }
                    else
                    {
                        Error(args.Actor, "rmc-police-hold-id");
                        return;
                    }
                    break;
                case RMCSentencingAction.Summary:
                    report.Summary = args.Text.Trim();
                    break;
                case RMCSentencingAction.AddCharge:
                    if (!_prototypes.TryIndex(report.LawCatalogue, out var cataloguePrototype) ||
                        !cataloguePrototype.TryGetComponent(out RMCMarineLawCatalogueComponent? catalogue,
                            EntityManager.ComponentFactory) ||
                        !catalogue.Laws.Contains(args.Text) || !RMCMarineLaw.TryGet(_prototypes, EntityManager.ComponentFactory, args.Text, out _) ||
                        report.Charges.Count >= RMCIncidentReport.MaxEntries)
                        return;
                    report.Charges.Add(new RMCIncidentCharge(args.Text));
                    break;
                case RMCSentencingAction.RelatedCrime:
                    if (args.Index < 0 || args.Index >= report.Charges.Count ||
                        !RMCMarineLaw.TryGet(_prototypes, EntityManager.ComponentFactory, report.Charges[args.Index].Law, out var aiding) ||
                        aiding.Punishment != RMCMarineLawPunishment.SameAsCrime ||
                        !_prototypes.TryIndex(report.LawCatalogue, out var relatedPrototype) ||
                        !relatedPrototype.TryGetComponent(out RMCMarineLawCatalogueComponent? relatedCatalogue,
                            EntityManager.ComponentFactory) ||
                        !relatedCatalogue.Laws.Contains(args.Text) ||
                        !RMCMarineLaw.TryGet(_prototypes, EntityManager.ComponentFactory, args.Text, out var related) ||
                        related.Punishment == RMCMarineLawPunishment.SameAsCrime)
                        return;
                    report.Charges[args.Index].RelatedCrime = args.Text;
                    break;
                case RMCSentencingAction.RemoveCharge:
                    if (args.Index < 0 || args.Index >= report.Charges.Count)
                        return;
                    report.Charges.RemoveAt(args.Index);
                    break;
                case RMCSentencingAction.AddWitness:
                    if (!TryHeldId(args.Actor, out var witness, out _) ||
                        string.IsNullOrWhiteSpace(witness.FullName))
                    {
                        Error(args.Actor, "rmc-police-hold-id");
                        return;
                    }
                    if (report.Witnesses.Count >= RMCIncidentReport.MaxEntries)
                        return;
                    report.Witnesses.Add(new RMCIncidentEntry(witness.FullName));
                    break;
                case RMCSentencingAction.AddEvidence:
                    if (!TryHeldItem(args.Actor, out var item))
                    {
                        Error(args.Actor, "rmc-police-hold-evidence");
                        return;
                    }
                    if (report.Evidence.Count >= RMCIncidentReport.MaxEntries)
                        return;
                    report.Evidence.Add(new RMCIncidentEntry(Name(item.Value)));
                    break;
                case RMCSentencingAction.EditWitness:
                case RMCSentencingAction.RemoveWitness:
                case RMCSentencingAction.EditEvidence:
                case RMCSentencingAction.RemoveEvidence:
                    var entries = args.Action is RMCSentencingAction.EditWitness or RMCSentencingAction.RemoveWitness
                        ? report.Witnesses : report.Evidence;
                    if (args.Index < 0 || args.Index >= entries.Count)
                        return;
                    if (args.Action is RMCSentencingAction.EditWitness or RMCSentencingAction.EditEvidence)
                        entries[args.Index].Notes = args.Text.Trim();
                    else
                        entries.RemoveAt(args.Index);
                    break;
                case RMCSentencingAction.Export:
                    if (!Export(ent, args.Actor, report))
                        return;
                    ent.Comp.Report = null;
                    break;
                default:
                    return;
            }
        }
        else
        {
            return;
        }

        ent.Comp.Revision++;
        LogChange(args.Actor, ent, $"incident report {args.Action}: {args.Text} {args.Index}");
        Refresh(ent);
    }

    private bool Export(Entity<RMCPoliceConsoleComponent> ent, EntityUid actor, RMCIncidentReport report)
    {
        if (report.LawCatalogue != _lawCatalogue || string.IsNullOrWhiteSpace(report.Suspect) ||
            !report.TryCalculateSentence(_prototypes, EntityManager.ComponentFactory, out var sentence))
        {
            Error(actor, "rmc-police-report-incomplete");
            return false;
        }

        var paper = Print(ent, actor, ent.Comp.ReportPaper, report.Suspect, FormatIncident(report));
        if (paper == null)
            return false;

        report.FinalSentence = sentence;
        EnsureComp<RMCIncidentReportPaperComponent>(paper.Value).Report = report.Clone();
        if (GetStation(ent) is { } station && report.RecordId is { } id &&
            report.RecordStation == GetNetEntity(station))
        {
            var key = new StationRecordKey(id, station);
            if (_records.TryGetRecord<RMCPoliceRecord>(key, out var record))
            {
                record.Incidents.Add(report.Clone());
                _records.Synchronize(key);
            }
        }

        return true;
    }

    private void OnInteractUsing(Entity<RMCPoliceConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.Sentencing || !CanUse(ent, args.User))
            return;

        if (TryComp(args.Used, out RMCIncidentReportPaperComponent? paper) && paper.Report != null)
        {
            args.Handled = true;
            if (paper.Report.LawCatalogue != _lawCatalogue ||
                !paper.Report.TryCalculateSentence(_prototypes, EntityManager.ComponentFactory, out _))
            {
                Error(args.User, "rmc-police-report-wrong-catalogue");
                return;
            }
            if (ent.Comp.Report != null)
            {
                Error(args.User, "rmc-police-report-in-progress");
                return;
            }
            ent.Comp.Report = paper.Report.Clone();
            ent.Comp.Report.Id = Guid.NewGuid();
            ent.Comp.Report.FinalSentence = null;
            ent.Comp.Report.Detention = null;
            ent.Comp.Revision++;
            LogChange(args.User, ent, $"imported incident report for {paper.Report.Suspect}");
            QueueDel(args.Used);
            Refresh(ent);
            _ui.TryOpenUi(ent.Owner, RMCPoliceUiKey.Key, args.User);
        }
        else if (HasComp<PaperComponent>(args.Used))
        {
            Error(args.User, "rmc-police-original-only");
            args.Handled = true;
        }
    }
}
