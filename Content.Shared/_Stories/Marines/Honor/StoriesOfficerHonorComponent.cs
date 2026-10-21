using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Marines.Honor;

/// <summary>
/// Allows an officer to call nearby marines to attention.
/// </summary>
[RegisterComponent]
public sealed partial class StoriesOfficerHonorComponent : Component
{
    [DataField]
    public EntProtoId Action = "STActionOfficerHonor";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [DataField]
    public float Range = 10f;

    [DataField]
    public bool CheckVisibility = true;

    [DataField]
    public List<LocId> Callouts = new()
    {
        "stories-officer-honor-attention-1",
        "stories-officer-honor-attention-2",
        "stories-officer-honor-attention-3",
    };

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan ForcedWhisperDuration = TimeSpan.FromSeconds(13);
}

[RegisterComponent]
public sealed partial class StoriesOfficerHonorReactionComponent : Component
{
    public EntityUid Target;
    public float Range;
    public bool CheckVisibility;
    public TimeSpan NextStepAt;
    public StoriesOfficerHonorReactionStep Step;
}

public enum StoriesOfficerHonorReactionStep : byte
{
    Unbuckle,
    Stand,
    Face,
    Salute,
}

[RegisterComponent]
public sealed partial class StoriesOfficerHonorForcedWhisperComponent : Component
{
    public TimeSpan ExpiresAt;
}

[RegisterComponent]
public sealed partial class StoriesMarineSilencedForcedWhisperComponent : Component
{
    public TimeSpan ExpiresAt;
}
