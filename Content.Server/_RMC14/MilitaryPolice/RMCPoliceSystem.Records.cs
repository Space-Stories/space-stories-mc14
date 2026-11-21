using System.Linq;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared.CriminalRecords;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Robust.Shared.Enums;

namespace Content.Server._RMC14.MilitaryPolice;

public sealed partial class RMCPoliceSystem
{
    private void OnRecordMessage(Entity<RMCPoliceConsoleComponent> ent, ref RMCPoliceRecordMsg args)
    {
        if (ent.Comp.Sentencing || !CanUse(ent, args.Actor) || args.Text.Length > MaxTextLength)
            return;

        var station = GetStation(ent);
        if (station == null)
        {
            Error(args.Actor, "rmc-police-disconnected");
            return;
        }

        if (args.Action == RMCPoliceRecordAction.CreateGeneral)
        {
            var key = _records.AddRecordEntry(station.Value, new GeneralStationRecord
            {
                Name = Loc.GetString("rmc-police-new-record-name"),
                Age = RMCPoliceRecord.GetAgeLimits(_prototypes, null).Min,
                JobTitle = Loc.GetString("rmc-police-unknown"),
            });
            _records.AddRecordEntry(key, new RMCPoliceRecord());
            _records.AddRecordEntry(key, new CriminalRecord());
            _records.Synchronize(key);
            _ui.ServerSendUiMessage(ent.Owner, RMCPoliceUiKey.Key, new RMCPoliceRecordSelectedMsg(key.Id), args.Actor);
            LogChange(args.Actor, ent, $"created personnel record {key.Id}");
            return;
        }

        if (args.Action == RMCPoliceRecordAction.LinkPerson)
        {
            if (!TryRegistrationSubject(ent, args.Actor, out var person))
            {
                Error(args.Actor, "rmc-police-link-person-required");
                return;
            }
            if (!_rmcRecords.TryRegister(station.Value, person, out var key, out var error, args.Id))
            {
                if (error is { } message)
                    Error(args.Actor, message);
                return;
            }
            _ui.ServerSendUiMessage(ent.Owner, RMCPoliceUiKey.Key, new RMCPoliceRecordSelectedMsg(key.Id), args.Actor);
            LogChange(args.Actor, ent, $"registered {ToPrettyString(person)} in personnel record {key.Id}");
            return;
        }

        if (args.Action is RMCPoliceRecordAction.PrintFingerprints or
            RMCPoliceRecordAction.ClearFingerprints or RMCPoliceRecordAction.EjectScanner)
        {
            ScannerAction(ent, args);
            return;
        }

        var recordKey = new StationRecordKey(args.Id, station.Value);
        if (!_records.TryGetRecord<GeneralStationRecord>(recordKey, out var general))
            return;

        if (args.Action == RMCPoliceRecordAction.CreateSecurity)
        {
            if (!_records.TryGetRecord<RMCPoliceRecord>(recordKey, out _))
            {
                _records.AddRecordEntry(recordKey, new RMCPoliceRecord());
                _records.Synchronize(recordKey);
            }
            return;
        }

        if (args.Action == RMCPoliceRecordAction.DeleteGeneral)
        {
            if (!HighCommand(args.Actor))
            {
                Error(args.Actor, "rmc-police-high-command-only");
                return;
            }

            _criminal.UpdateCriminalIdentity(general.Name, SecurityStatus.None);
            _records.RemoveRecord(recordKey);
            LogChange(args.Actor, ent, $"deleted personnel record {general.Name} ({args.Id})");
            return;
        }

        if (args.Action == RMCPoliceRecordAction.EditGeneral)
        {
            EditGeneral(ent, args, recordKey, general);
            return;
        }

        _records.TryGetRecord<RMCPoliceRecord>(recordKey, out var security);
        if (args.Action == RMCPoliceRecordAction.Print)
        {
            Print(ent, args.Actor, ent.Comp.RecordPaper, general.Name,
                FormatRecord(args.Id, general, security ?? new RMCPoliceRecord()));
            return;
        }

        if (security == null)
            return;

        switch (args.Action)
        {
            case RMCPoliceRecordAction.Status:
                if (!Enum.IsDefined(typeof(RMCPoliceStatus), (byte) args.Index) || args.Index is < 0 or > 255)
                    return;
                security.Status = (RMCPoliceStatus) args.Index;
                UpdateStatus(recordKey, security, args.Actor);
                break;
            case RMCPoliceRecordAction.PhysicalStatus:
                if (args.Index is < 0 or > 2)
                    return;
                security.PhysicalStatus = (RMCPolicePhysicalStatus) args.Index;
                break;
            case RMCPoliceRecordAction.MentalStatus:
                if (args.Index is < 0 or > 3)
                    return;
                security.MentalStatus = (RMCPoliceMentalStatus) args.Index;
                break;
            case RMCPoliceRecordAction.Comment:
                var text = args.Text.Trim();
                if (text.Length is < 1 or > MaxCommentLength || security.Comments.Count >= MaxComments)
                    return;
                security.Comments.Add(new RMCPoliceComment
                {
                    Text = text,
                    Author = Officer(args.Actor),
                    CreatedAt = _ticker.RoundDuration(),
                });
                break;
            case RMCPoliceRecordAction.DeleteComment:
                if (args.Index < 0 || args.Index >= security.Comments.Count)
                    return;
                var comment = security.Comments[args.Index];
                if (comment.DeletedBy != null)
                    return;
                comment.DeletedBy = Officer(args.Actor);
                comment.DeletedAt = _ticker.RoundDuration();
                break;
            default:
                return;
        }

        LogChange(args.Actor, ent, $"{args.Action} on record {general.Name} ({args.Id}): {args.Text} {args.Index}");
        _records.Synchronize(recordKey);
    }

    private void EditGeneral(Entity<RMCPoliceConsoleComponent> ent, RMCPoliceRecordMsg args,
        StationRecordKey key, GeneralStationRecord general)
    {
        var value = args.Text.Trim();
        switch (args.Field)
        {
            case "name":
                if (value.Length is < 1 or > 49 || value.Any(character =>
                        !char.IsLetter(character) && character is not (' ' or '\'' or '-')))
                {
                    Error(args.Actor, "rmc-police-invalid-name");
                    return;
                }
                _criminal.UpdateCriminalIdentity(general.Name, SecurityStatus.None);
                general.Name = value;
                if (_records.TryGetRecord<RMCPoliceRecord>(key, out var security))
                    UpdateStatus(key, security, args.Actor);
                break;
            case "age":
                _records.TryGetRecord<RMCPoliceRecord>(key, out var personal);
                var limits = RMCPoliceRecord.GetAgeLimits(_prototypes, personal?.Portrait);
                if (!int.TryParse(value, out var age) || age < limits.Min || age > limits.Max)
                {
                    Error(args.Actor, "rmc-police-invalid-age", ("min", limits.Min), ("max", limits.Max));
                    return;
                }
                general.Age = age;
                break;
            case "gender":
                if (!Enum.TryParse<Gender>(value, out var gender) || !Enum.IsDefined(gender))
                    return;
                general.Gender = gender;
                break;
            case "rank":
                if (!HighCommand(args.Actor))
                {
                    Error(args.Actor, "rmc-police-high-command-only");
                    return;
                }
                if (!_prototypes.TryIndex<Content.Shared.Roles.JobPrototype>(value, out var job) ||
                    !RMCPoliceRecord.CanAssignJob(_prototypes, job))
                    return;
                general.JobPrototype = job.ID;
                general.JobTitle = job.LocalizedName;
                general.JobIcon = job.Icon;
                break;
            default:
                return;
        }

        LogChange(args.Actor, ent, $"edited record {key.Id}: {args.Field} = {value}");
        _records.Synchronize(key);
    }

    public bool IsReportSubject(EntityUid actor, RMCIncidentReport report)
    {
        if (report.RecordId is { } id && report.RecordStation is { } netStation && TryGetEntity(netStation, out var station))
        {
            if (_rmcRecords.TryGetRecordKey(actor, station, out var key))
                return key.Id == id;
        }

        // Reports made from an unregistered ID still identify the subject by their recorded name.
        return string.Equals(Name(actor), report.Suspect, StringComparison.Ordinal);
    }

    private bool TryRegistrationSubject(EntityUid console, EntityUid actor, out EntityUid person)
    {
        person = default;
        if (CompOrNull<PullerComponent>(actor)?.Pulling is not { } pulled || TerminatingOrDeleted(pulled) ||
            !_interaction.InRangeUnobstructed(actor, pulled) ||
            !_interaction.InRangeUnobstructed(console, pulled))
            return false;
        person = pulled;
        return true;
    }
}
