using Content.Shared.FixedPoint;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Tracks continuous fire time on the M134C-JLCW - unlike a decaying heat gauge (OverheatComponent), it deals real escalating damage past DamageStartAfter, with no firing-block until DisableAtDamage forces an actual weld. Server-side bookkeeping; not networked.</summary>
[RegisterComponent]
[Access(typeof(JuggernautBarrelHeatSystem))]
public sealed partial class JuggernautBarrelHeatComponent : Component
{
    /// <summary>Gap between shots after which the continuous-fire timer resets.</summary>
    [DataField]
    public TimeSpan SustainedFireGrace = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan DamageStartAfter = TimeSpan.FromSeconds(5);

    [DataField]
    public FixedPoint2 BaseDamagePerTick = FixedPoint2.New(5);

    [DataField]
    public FixedPoint2 DamagePerTickIncrease = FixedPoint2.New(3);

    [DataField]
    public FixedPoint2 DisableAtDamage = FixedPoint2.New(150);

    public TimeSpan? FiringSince;

    public TimeSpan LastShotAt;

    public TimeSpan? NextDamageTickAt;

    public int TicksDealt;
}
