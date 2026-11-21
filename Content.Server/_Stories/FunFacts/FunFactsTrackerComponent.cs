using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Stories.FunFacts;

[RegisterComponent]
[Access(typeof(FunFactsSystem))]
public sealed partial class FunFactsTrackerComponent : Component
{
    [ViewVariables]
    public NetUserId? Player;

    [ViewVariables]
    public string Name = string.Empty;

    [ViewVariables]
    public string Gender = "male";

    [ViewVariables]
    public bool Xeno;

    [ViewVariables]
    public bool ExcludedCaste;

    [ViewVariables]
    public bool Dead;

    [ViewVariables]
    public string? DeathCause;

    [ViewVariables]
    public bool Transferred;

    [ViewVariables]
    public EntityUid? LastAttacker;

    [ViewVariables]
    public TimeSpan LastAttackedAt;

    [ViewVariables]
    public TimeSpan? CritStartedAt;

    [ViewVariables]
    public bool Burning;

    [ViewVariables]
    public GameTick LastHitTick;

    [ViewVariables]
    public EntityUid? LastHitTarget;

    [ViewVariables]
    public FunFactsStats Stats = new();
}

public sealed class FunFactsStats
{
    public int Kills;
    public float DamageTaken;
    public float DamageDealt;
    public float FriendlyDamage;
    public float Healed;
    public int Revives;
    public int TimesRevived;
    public TimeSpan CritTime;
    public int Evolutions;
    public int ShotsFired;
    public int ShotsHit;
    public int Surgeries;
    public int Built;
    public int Dogtags;
    public int Grenades;
    public int Ignited;
    public int Eaten;
    public int Nested;
    public int Infected;
    public int ResinBuilt;
    public int WeedsPlanted;
    public int AcidApplied;
    public int XenoHeals;
    public int Knockdowns;
    public int Rescues;
}
