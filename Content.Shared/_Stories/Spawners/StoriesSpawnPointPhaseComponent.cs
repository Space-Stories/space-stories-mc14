namespace Content.Shared._Stories.Spawners;

[RegisterComponent]
[Access(Other = AccessPermissions.Read)]
public sealed partial class StoriesSpawnPointPhaseComponent : Component
{
    [DataField]
    public bool RoundStart = true;

    [DataField]
    public bool LateJoin = true;
}
