namespace Content.Shared._Stories.FunFacts;

[RegisterComponent]
public sealed partial class FunFactComponent : Component
{
    [DataField(required: true)]
    public LocId Message;

    [DataField(required: true)]
    public FunFactStat Stat;

    [DataField]
    public float Min = 1;

    [DataField]
    public bool Humans = true;

    [DataField]
    public bool Xenos = true;

    [DataField]
    public bool AllowExcludedCaste;
}

public enum FunFactStat
{
    Kills,
    DamageTaken,
    DamageDealt,
    FriendlyDamage,
    CritMinutes,
    Revives,
    TimesRevived,
    Evolutions,
    ShotsFired,
    Healed,
    Surgeries,
    Built,
    Dogtags,
    Grenades,
    Ignited,
    Eaten,
    Nested,
    Infected,
    ResinBuilt,
    WeedsPlanted,
    AcidApplied,
    XenoHeals,
    Knockdowns,
    Rescues,
    Deaths,
    LarvaeBurst,
}
