using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Hermes;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CN20GasComponent : Component
{
    [DataField] public bool AffectsXenos = true;
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(1);
    [DataField] public DamageSpecifier OxygenDamage = new() { DamageDict = { ["Asphyxiation"] = 12 } };
    [DataField] public TimeSpan BlindDuration = TimeSpan.FromSeconds(2);
    [DataField] public TimeSpan DeafDuration = TimeSpan.FromSeconds(6);
    [DataField] public TimeSpan SlowDuration = TimeSpan.FromSeconds(3);
    [DataField] public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(4);
    [DataField] public float KnockdownChance = 0.35f;
    [DataField] public float DropChance = 0.75f;
    [DataField] public EntityWhitelist? Resistant;
    [DataField] public float ResistanceChance = 0.75f;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTick;
    public readonly HashSet<EntityUid> Touching = [];
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CN20ExposureComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextEffect;
}
