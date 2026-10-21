using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.MilitaryPolice;

[Serializable, NetSerializable]
public enum RMCPoliceStatus : byte
{
    None,
    Arrest,
    Incarcerated,
    Released,
    Suspect,
    NJP,
}

[Serializable, NetSerializable]
public enum RMCPolicePhysicalStatus : byte
{
    Active,
    Inactive,
    Unknown,
}

[Serializable, NetSerializable]
public enum RMCPoliceMentalStatus : byte
{
    Stable,
    Watch,
    Unstable,
    Insane,
}

/// <summary>
/// CM security data supplements the general station record, preserving its ID card key.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class RMCPoliceRecord
{
    public static bool CanAssignJob(IPrototypeManager prototypes, JobPrototype job) =>
        job.IsCM && prototypes.TryIndex<PlayTimeTrackerPrototype>(job.PlayTimeTracker, out var tracker) && !tracker.IsXeno;

    public static (int Min, int Max) GetAgeLimits(IPrototypeManager prototypes, HumanoidCharacterProfile? portrait)
    {
        var species = prototypes.Index<SpeciesPrototype>(portrait?.Species ?? SharedHumanoidAppearanceSystem.DefaultSpecies);
        return (species.MinAge, species.MaxAge);
    }

    [DataField]
    public HumanoidCharacterProfile? Portrait;

    [DataField]
    public RMCPoliceStatus Status;

    [DataField]
    public RMCPolicePhysicalStatus PhysicalStatus;

    [DataField]
    public RMCPoliceMentalStatus MentalStatus;

    [DataField]
    public string MajorCrimes = string.Empty;

    [DataField]
    public List<RMCPoliceComment> Comments = new();

    [DataField]
    public List<RMCIncidentReport> Incidents = new();
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class RMCPoliceComment
{
    [DataField]
    public string Text = string.Empty;

    [DataField]
    public string Author = string.Empty;

    [DataField]
    public TimeSpan CreatedAt;

    [DataField]
    public string? DeletedBy;

    [DataField]
    public TimeSpan? DeletedAt;
}
