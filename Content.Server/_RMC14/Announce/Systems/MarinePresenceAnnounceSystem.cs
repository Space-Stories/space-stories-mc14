using System.Globalization;
using System.Linq;
using Content.Server._RMC14.Marines;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Announce;

public sealed class MarinePresenceAnnounceSystem : EntitySystem
{
    [Dependency] private readonly ARESCoreSystem _aresCore = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedRankSystem _rank = default!;
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

    [ValidatePrototypeId<RadioChannelPrototype>]
    private static readonly ProtoId<RadioChannelPrototype> CommonChannel = "MarineCommon";

    public void AnnounceLateJoin(bool lateJoin, bool silent, EntityUid mob, string jobId, string jobName, JobPrototype jobPrototype)
    {
        if (!lateJoin || silent)
            return;

        var ares = _aresCore.EnsureMarineARES();
        var titleJobName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName);

        if (jobPrototype.JoinNotifyCrew)
        {
            var fullRankName = _rank.GetSpeakerFullRankName(mob) ?? Name(mob);
            _marineAnnounce.AnnounceARESStaging(ares,
                Loc.GetString("rmc-latejoin-arrival-announcement-special",
                    ("character", fullRankName)),
                jobPrototype.LatejoinArrivalSound,
                null,
                playTTS: false); // Stories-TTS-Skip
            return;
        }

        var rankName = _rank.GetSpeakerRankName(mob) ?? Name(mob);
        var message = Loc.GetString(jobPrototype.LateJoinAnnouncement ?? "rmc-latejoin-arrival-announcement", // Stories-LateJoinAnnounce
            ("character", rankName),
            ("entity", mob),
            ("job", titleJobName));

        // Stories-LateJoinAnnounce-Start
        foreach (var channel in jobPrototype.LateJoinChannels)
        {
            _marineAnnounce.AnnounceRadio(ares, message, channel, playTTS: false);
        }
        // Stories-LateJoinAnnounce-End

        AnnounceToJobChannels(ares, mob, jobId, message);
    }

    public void AnnounceEarlyLeave(Entity<CryostorageContainedComponent> ent, uint? recordId, EntityUid? station, string jobName)
    {
        var ares = _aresCore.EnsureMarineARES();
        var rankName = _rank.GetSpeakerRankName(ent.Owner) ?? Name(ent.Owner);
        var titleJobName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName);
        var message = Loc.GetString("rmc-earlyleave-cryo-announcement",
            ("character", rankName),
            ("entity", ent.Owner),
            ("job", titleJobName));

        if (!TryComp<StationRecordsComponent>(station, out var stationRecords))
            return;

        JobPrototype? jobProto = null;
        if (recordId != null && station != null)
        {
            var key = new StationRecordKey(recordId.Value, station.Value);
            if (_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var entry, stationRecords) &&
                !string.IsNullOrWhiteSpace(entry.JobPrototype))
            {
                _prototypeManager.TryIndex(entry.JobPrototype, out jobProto);
            }
        }

        if (jobProto != null)
            AnnounceToJobChannels(ares, ent.Owner, jobProto.ID, message);
        else
            _marineAnnounce.AnnounceRadio(ares, message, CommonChannel, playTTS: false); // Stories-TTS-Skip
    }

    private void AnnounceToJobChannels(EntityUid ares, EntityUid mob, string jobId, string message)
    {
        var departmentPrototypes = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().ToList();
        var processedChannels = new HashSet<ProtoId<RadioChannelPrototype>>();
        var departmentChannelFound = false;
        var isHead = false;

        foreach (var department in departmentPrototypes)
        {
            if (!department.Roles.Contains(jobId))
                continue;

            if (department.HeadOfDepartment == jobId)
                isHead = true;

            var channelId = department.DepartmentRadio;
            // Fall back to squad radio for combat roles with no department channel
            if (channelId == null &&
                _squad.TryGetMemberSquad(mob, out var squad) &&
                squad.Comp.Radio != null)
            {
                channelId = squad.Comp.Radio;
            }

            if (channelId == null || !processedChannels.Add(channelId.Value))
                continue;

            departmentChannelFound = true;
            _marineAnnounce.AnnounceRadio(ares, message, channelId.Value, playTTS: false); // Stories-TTS-Skip
        }

        // Heads always also receive the common channel announcement
        if (!departmentChannelFound || isHead)
            _marineAnnounce.AnnounceRadio(ares, message, CommonChannel, playTTS: false); // Stories-TTS-Skip
    }
}
