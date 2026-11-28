using Content.Server.Access.Components;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.ARES.Interface;

public sealed class AresIdentitySystem : EntitySystem
{
    [Dependency] private readonly StationRecordsSystem _records = default!;

    public bool TryGetJob(Entity<IdCardComponent> card, out ProtoId<JobPrototype> job)
    {
        if (TryComp(card, out StationRecordKeyStorageComponent? storage) && storage.Key is { } key &&
            _records.TryGetRecord<GeneralStationRecord>(key, out var record) && !string.IsNullOrEmpty(record.JobPrototype))
        {
            job = new ProtoId<JobPrototype>(record.JobPrototype);
            return true;
        }

        if (card.Comp.JobPrototype is { } assigned && !string.IsNullOrEmpty(assigned.Id))
        {
            job = new ProtoId<JobPrototype>(assigned.Id);
            return true;
        }

        if (TryComp(card, out PresetIdCardComponent? preset) && preset.JobName is { } presetJob)
        {
            job = presetJob;
            return true;
        }

        job = default;
        return false;
    }
}
