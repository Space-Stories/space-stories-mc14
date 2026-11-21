using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Evolution;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(XenoEvolutionSystem))]
public sealed partial class XenoRaffleCandidateComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Target;

    // Stories-XenoEvolutionQueue-Start
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Path = new();

    public EntProtoId CurrentTarget => Path.Count > 0 ? Path[0] : Target;
    // Stories-XenoEvolutionQueue-End

    [DataField, AutoNetworkedField]
    public int Tier;

    [DataField, AutoNetworkedField]
    public bool Leapfrog;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? GraceUntil;

    [DataField]
    public FixedPoint2 GraceDamage;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? GraceHealedAt;

    [DataField, AutoNetworkedField]
    public bool Evolving;

    public DoAfterId? EvolutionDoAfter; // Stories-XenoEvolutionQueue
    public XenoEvolutionDoAfterEvent? EvolutionEvent; // Stories-XenoEvolutionQueue

    [DataField, AutoNetworkedField]
    public bool Skipped;

    [DataField, AutoNetworkedField]
    public FixedPoint2 RaffleCost;

    [DataField, AutoNetworkedField]
    public FixedPoint2 RaffleProgress;
}
