using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.MilitaryPolice;

/// <summary>
/// CM structures that leave a clue when a human damages them enough to break or destroy them.
/// </summary>
[RegisterComponent]
public sealed partial class RMCPoliceClueOnDestructionComponent : Component
{
    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public bool MainshipOnly;

    [DataField]
    public EntityUid? DamageOrigin;

    [DataField]
    public bool Created;
}
