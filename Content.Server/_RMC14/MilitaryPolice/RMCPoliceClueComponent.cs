using Content.Shared._RMC14.MilitaryPolice;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._RMC14.MilitaryPolice;

[RegisterComponent]
public sealed partial class RMCPoliceClueComponent : Component
{
    [DataField]
    public RMCPoliceClue Clue = new();

    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan RevealDuration = TimeSpan.FromMinutes(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan RevealedUntil;
}
