using Content.Server._RMC14.StationRecords;
using Content.Server.StationRecords;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.CriminalRecords;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Security;
using Content.Shared.StationRecords;

namespace Content.Server._RMC14.MilitaryPolice;

public sealed partial class RMCPoliceSystem
{
    private void OnEquipped(Entity<InventoryComponent> ent, ref DidEquipEvent args)
    {
        if (args.Slot == "id")
            RefreshHud(ent);
    }

    private void OnUnequipped(Entity<InventoryComponent> ent, ref DidUnequipEvent args)
    {
        if (args.Slot == "id")
            RefreshHud(ent);
    }

    private void OnIdentityChanged(Entity<IdentityComponent> ent, ref IdentityChangedEvent args) => RefreshHud(ent);

    private void RefreshHud()
    {
        var query = EntityQueryEnumerator<InventoryComponent>();
        while (query.MoveNext(out var uid, out _))
            RefreshHud(uid);
    }

    private void RefreshHud(EntityUid wearer)
    {
        if (TerminatingOrDeleted(wearer))
            return;

        if (!TryHudKey(wearer, out var key) ||
            !_records.TryGetRecord<RMCPoliceRecord>(key, out var record))
        {
            RemComp<RMCPoliceStatusComponent>(wearer);
            return;
        }

        var status = EnsureComp<RMCPoliceStatusComponent>(wearer);
        if (status.Status == record.Status)
            return;
        status.Status = record.Status;
        Dirty(wearer, status);
    }

    private bool TryHudKey(EntityUid person, out StationRecordKey key)
    {
        // Use the registered identity even if the person leaves the ship or exchanges their card.
        if (TryComp<RMCStationRecordIdentityComponent>(person, out var registered) &&
            _rmcRecords.TryGetRecordKey(person, registered.ActiveStation, out key))
            return true;
        if (_rmcRecords.TryGetRecordKey(person, _station.GetOwningStation(person), out key))
            return true;
        if (TryWornId(person, out var card) && CompOrNull<StationRecordKeyStorageComponent>(card)?.Key is { } cardKey &&
            !TerminatingOrDeleted(cardKey.OriginStation) &&
            TryComp<StationRecordsComponent>(cardKey.OriginStation, out var records) &&
            _records.TryGetRecord<GeneralStationRecord>(cardKey, out _, records))
        {
            if (!_rmcRecords.TryGetRecordKey(person, cardKey.OriginStation, out key))
                key = cardKey;
            return true;
        }
        // Away from the ship, a person without an ID can still have one unambiguous personnel file.
        if (TryComp<RMCStationRecordIdentityComponent>(person, out var identity))
        {
            var found = false;
            foreach (var (station, id) in identity.Records)
            {
                var candidate = new StationRecordKey(id, station);
                if (TerminatingOrDeleted(station) ||
                    !TryComp<StationRecordsComponent>(station, out var stationRecords) ||
                    !_records.TryGetRecord<RMCPoliceRecord>(candidate, out _, stationRecords))
                    continue;
                if (found)
                    return false;
                key = candidate;
                found = true;
            }
            return found;
        }
        return false;
    }

    private void UpdateStatus(StationRecordKey key, RMCPoliceRecord security, EntityUid actor)
    {
        if (!_records.TryGetRecord<CriminalRecord>(key, out var criminal))
        {
            criminal = new CriminalRecord();
            _records.AddRecordEntry(key, criminal);
        }

        var status = ToSecurityStatus(security.Status);
        _criminal.OverwriteStatus(key, criminal, status,
            status is SecurityStatus.Wanted or SecurityStatus.Suspected ? security.MajorCrimes : null, Officer(actor));
    }

    private static SecurityStatus ToSecurityStatus(RMCPoliceStatus status) => status switch
    {
        RMCPoliceStatus.Arrest => SecurityStatus.Wanted,
        RMCPoliceStatus.Incarcerated => SecurityStatus.Detained,
        RMCPoliceStatus.Released => SecurityStatus.Discharged,
        RMCPoliceStatus.Suspect => SecurityStatus.Suspected,
        _ => SecurityStatus.None,
    };

    private void OnInsubordination(Entity<OverwatchConsoleComponent> ent, ref RMCOverwatchInsubordinationMsg args)
    {
        if (!CanUse(ent, args.Actor) || ent.Comp.Squad is not { } selected ||
            !TryGetEntity(args.Target, out var target) ||
            !TryComp(target, out SquadMemberComponent? member) || member.Squad != GetEntity(selected) ||
            !_rmcRecords.TryGetRecordKey(target.Value, GetStation(ent), out var key) ||
            !_records.TryGetRecord<RMCPoliceRecord>(key, out var record))
            return;

        if (record.Status == RMCPoliceStatus.Incarcerated)
        {
            _popup.PopupEntity(Loc.GetString("rmc-police-insubordination-incarcerated"), ent, args.Actor);
            return;
        }

        var charge = Loc.GetString("rmc-police-insubordination-charge");
        var alreadyReported = record.MajorCrimes.Contains(charge, StringComparison.Ordinal);
        if (alreadyReported && record.Status == RMCPoliceStatus.Arrest)
        {
            _popup.PopupEntity(Loc.GetString("rmc-police-insubordination-already-wanted"), ent, args.Actor);
            return;
        }

        if (!alreadyReported)
            record.MajorCrimes = string.IsNullOrWhiteSpace(record.MajorCrimes) ? charge : $"{record.MajorCrimes}\n{charge}";
        record.Status = RMCPoliceStatus.Arrest;
        UpdateStatus(key, record, args.Actor);
        LogChange(args.Actor, ent, $"reported {ToPrettyString(target.Value):player} for insubordination");
        _popup.PopupEntity(Loc.GetString("rmc-police-insubordination-reported"), target.Value, target.Value);
        _popup.PopupEntity(Loc.GetString("rmc-police-insubordination-confirmed"), ent, args.Actor);
        _records.Synchronize(key);
    }
}
