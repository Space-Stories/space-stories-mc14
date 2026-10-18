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
    public int Range = 10;

    [DataField]
    public bool CheckVisibility = true;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextHonorAt;
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
