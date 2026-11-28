using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.MilitaryPolice;

/// <summary>
/// Evidence captured at the time of an incident, rather than the suspect's current ID or name.
/// </summary>
[DataDefinition]
public sealed partial class RMCPoliceClue
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Rank = string.Empty;

    [DataField]
    public string Squad = string.Empty;

    [DataField]
    public string Fingerprint = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public NetEntity? Source;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? ExpiresAt;
}

[Serializable, NetSerializable]
public enum RMCPoliceClueVisuals : byte
{
    Scanning,
    Stored,
}

[Serializable, NetSerializable]
public sealed partial class RMCPoliceScanDoAfterEvent : SimpleDoAfterEvent;
