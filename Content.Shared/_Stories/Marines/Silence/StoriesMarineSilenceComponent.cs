using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Marines.Silence;

/// <summary>
/// Allows an authorized marine in a command role to order lower-ranked marines to be silent.
/// Military Police intentionally do not receive this component.
/// </summary>
[RegisterComponent]
public sealed partial class StoriesMarineSilenceComponent : Component
{
    [DataField]
    public EntProtoId Action = "STActionMarineSilence";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [DataField]
    public StoriesMarineSilenceAuthority Authority = StoriesMarineSilenceAuthority.Officer;

    [DataField]
    public float Range = 10f;

    [DataField]
    public bool CheckVisibility = true;

    [DataField]
    public List<LocId> Callouts = new();

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

}

public enum StoriesMarineSilenceAuthority : byte
{
    Officer,
    Sergeant,
}
