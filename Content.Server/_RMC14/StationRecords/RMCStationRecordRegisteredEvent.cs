using Content.Shared.Preferences;
using Content.Shared.StationRecords;

namespace Content.Server._RMC14.StationRecords;

/// <summary>
/// Registration succeeded for a present person. Record extensions can ensure their data exists
/// for new, prepared and already linked records without replacing an existing extension.
/// </summary>
public sealed class RMCStationRecordRegisteredEvent(
    EntityUid person,
    StationRecordKey key,
    HumanoidCharacterProfile profile)
{
    public readonly EntityUid Person = person;
    public readonly StationRecordKey Key = key;
    public readonly HumanoidCharacterProfile Profile = profile;
}
