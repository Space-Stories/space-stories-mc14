namespace Content.Server._RMC14.StationRecords;

/// <summary>
/// Links a registered person to their personnel records independently of their current ID card.
/// </summary>
[RegisterComponent]
public sealed partial class RMCStationRecordIdentityComponent : Component
{
    [DataField]
    public Dictionary<EntityUid, uint> Records = new();

    /// <summary>The ship on which this person most recently completed registration.</summary>
    [DataField]
    public EntityUid? ActiveStation;
}
