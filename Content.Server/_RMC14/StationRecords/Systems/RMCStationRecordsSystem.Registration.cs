using System.Linq;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Content.Shared.StationRecords;

namespace Content.Server._RMC14.StationRecords.Systems;

public sealed partial class RMCStationRecordsSystem
{
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    private void OnRecordCreated(AfterGeneralRecordCreatedEvent args)
    {
        if (string.IsNullOrEmpty(args.Record.Fingerprint))
            return;

        var query = EntityQueryEnumerator<FingerprintComponent>();
        while (query.MoveNext(out var person, out var fingerprint))
        {
            if (fingerprint.Fingerprint == args.Record.Fingerprint &&
                (string.IsNullOrEmpty(args.Record.DNA) || CompOrNull<DnaComponent>(person)?.DNA == args.Record.DNA))
            {
                var identity = EnsureComp<RMCStationRecordIdentityComponent>(person);
                identity.Records[args.Key.OriginStation] = args.Key.Id;
                identity.ActiveStation ??= args.Key.OriginStation;
            }
        }
    }

    private void OnRecordRemoved(RecordRemovedEvent args)
    {
        var query = EntityQueryEnumerator<RMCStationRecordIdentityComponent>();
        while (query.MoveNext(out _, out var identity))
        {
            if (identity.Records.TryGetValue(args.Key.OriginStation, out var id) && id == args.Key.Id)
            {
                identity.Records.Remove(args.Key.OriginStation);
                if (identity.ActiveStation == args.Key.OriginStation)
                    identity.ActiveStation = null;
            }
        }
    }

    /// <summary>
    /// Finds a person's record on a particular ship without relying on their name or worn ID.
    /// Biometric lookup also covers records created before the person was explicitly linked.
    /// </summary>
    public bool TryGetRecordKey(EntityUid person, EntityUid? station, out StationRecordKey key)
    {
        key = default;
        if (station == null || TerminatingOrDeleted(station.Value) ||
            !TryComp<StationRecordsComponent>(station.Value, out var records))
            return false;

        if (TryComp<RMCStationRecordIdentityComponent>(person, out var identity) &&
            identity.Records.TryGetValue(station.Value, out var id))
        {
            key = new StationRecordKey(id, station.Value);
            if (_record.TryGetRecord<GeneralStationRecord>(key, out _, records))
                return true;
            identity.Records.Remove(station.Value);
            if (identity.ActiveStation == station)
                identity.ActiveStation = null;
        }

        var fingerprint = CompOrNull<FingerprintComponent>(person)?.Fingerprint;
        if (string.IsNullOrEmpty(fingerprint))
            return false;

        var dna = CompOrNull<DnaComponent>(person)?.DNA;
        var found = false;
        foreach (var (recordId, record) in _record.GetRecordsOfType<GeneralStationRecord>(station.Value, records))
        {
            if (record.Fingerprint != fingerprint || !string.IsNullOrEmpty(record.DNA) && record.DNA != dna)
                continue;
            if (found)
            {
                key = default;
                return false;
            }
            key = new StationRecordKey(recordId, station.Value);
            found = true;
        }

        if (found)
        {
            var linked = EnsureComp<RMCStationRecordIdentityComponent>(person);
            linked.Records[station.Value] = key.Id;
            linked.ActiveStation ??= station;
        }
        return found;
    }

    /// <summary>
    /// Registers a physical person, optionally linking a prepared general record. Callers must
    /// enforce their console's access and interaction checks before invoking this operation.
    /// Existing record extensions and edited personal data are preserved on repeat registration.
    /// </summary>
    public bool TryRegister(EntityUid station, EntityUid person, out StationRecordKey key, out LocId? error,
        uint? preparedRecord = null)
    {
        key = default;
        error = "rmc-records-invalid-person";
        if (!HasComp<StationRecordsComponent>(station) ||
            !TryComp<HumanoidAppearanceComponent>(person, out var humanoid) ||
            !TryComp<FingerprintComponent>(person, out var fingerprint) ||
            string.IsNullOrEmpty(fingerprint.Fingerprint) || string.IsNullOrWhiteSpace(Name(person)))
            return false;

        var registered = TryGetRecordKey(person, station, out key);
        error = "rmc-records-already-registered";
        if (registered && preparedRecord != null && preparedRecord != key.Id)
            return false;

        // Conflicting biometrics must be resolved instead of linking another duplicate.
        error = "rmc-records-ambiguous-person";
        if (!registered && _record.GetRecordsOfType<GeneralStationRecord>(station)
                .Any(entry => entry.Item2.Fingerprint == fingerprint.Fingerprint))
            return false;

        EntityUid? card = null;
        if (_inventory.TryGetSlotEntity(person, "id", out var worn) &&
            _idCard.TryGetIdCard(worn.Value, out var wornCard))
        {
            card = wornCard;
            var idCard = wornCard.Comp;
            error = "rmc-records-wrong-id";
            if (CompOrNull<StationRecordKeyStorageComponent>(wornCard)?.Key is { } cardKey &&
                _record.TryGetRecord<GeneralStationRecord>(cardKey, out _))
            {
                if (!TryGetRecordKey(person, cardKey.OriginStation, out var ownerKey) || !ownerKey.Equals(cardKey))
                    return false;
            }
            else if (!string.IsNullOrWhiteSpace(idCard.FullName) &&
                     !string.Equals(idCard.FullName, Name(person), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        GeneralStationRecord general;
        var created = false;
        var linked = false;
        if (registered)
        {
            _record.TryGetRecord(key, out general!);
        }
        else if (preparedRecord is { } recordId)
        {
            key = new StationRecordKey(recordId, station);
            error = "rmc-records-record-linked";
            if (!_record.TryGetRecord(key, out general!) ||
                !string.IsNullOrEmpty(general.Fingerprint) || !string.IsNullOrEmpty(general.DNA))
                return false;
            linked = true;
        }
        else
        {
            general = new GeneralStationRecord();
            key = _record.AddRecordEntry(station, general);
            created = true;
            linked = true;
        }

        var profile = GetProfile(person, humanoid);
        if (linked)
        {
            general.Name = profile.Name;
            general.Age = profile.Age;
            general.Species = profile.Species;
            general.Gender = profile.Gender;
            general.Fingerprint = fingerprint.Fingerprint;
            general.DNA = CompOrNull<DnaComponent>(person)?.DNA;
            if (string.IsNullOrEmpty(general.JobPrototype))
            {
                if (_jobs.MindTryGetJob(CompOrNull<MindContainerComponent>(person)?.Mind, out var job) && job.IsCM)
                {
                    general.JobPrototype = job.ID;
                    general.JobTitle = job.LocalizedName;
                    general.JobIcon = job.Icon;
                    general.DisplayPriority = job.RealDisplayWeight;
                }
                else
                    general.JobTitle = Loc.GetString("rmc-records-unknown-position");
            }
            if (_squad.TryGetMemberSquad(person, out var squad))
            {
                general.Squad = Name(squad);
                general.SquadColor = squad.Comp.Color;
            }
        }

        var identity = EnsureComp<RMCStationRecordIdentityComponent>(person);
        identity.Records[station] = key.Id;
        identity.ActiveStation = station;
        if (card is { } id)
        {
            EnsureComp<StationRecordKeyStorageComponent>(id);
            _record.SetIdKey(id, key);
            if (string.IsNullOrWhiteSpace(Comp<IdCardComponent>(id).FullName))
                _idCard.TryChangeFullName(id, profile.Name);
        }
        if (created)
            RaiseLocalEvent(new AfterGeneralRecordCreatedEvent(key, general, profile));
        RaiseLocalEvent(new RMCStationRecordRegisteredEvent(person, key, profile));
        _record.Synchronize(key);
        error = null;
        return true;
    }

    private HumanoidCharacterProfile GetProfile(EntityUid person, HumanoidAppearanceComponent humanoid)
    {
        humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Hair, out var hair);
        humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.FacialHair, out var facialHair);
        return new HumanoidCharacterProfile
        {
            Name = Name(person),
            Age = humanoid.Age,
            Species = humanoid.Species,
            Appearance = new HumanoidCharacterAppearance(
                hair?.FirstOrDefault()?.MarkingId ?? HairStyles.DefaultHairStyle,
                hair?.FirstOrDefault()?.MarkingColors.FirstOrDefault() ?? Color.Black,
                facialHair?.FirstOrDefault()?.MarkingId ?? HairStyles.DefaultFacialHairStyle,
                facialHair?.FirstOrDefault()?.MarkingColors.FirstOrDefault() ?? Color.Black,
                humanoid.EyeColor,
                humanoid.SkinColor,
                humanoid.MarkingSet.Markings.Values.SelectMany(markings => markings)
                    .Select(marking => new Marking(marking)).ToList()),
        }.WithSex(humanoid.Sex).WithGender(humanoid.Gender);
    }
}
