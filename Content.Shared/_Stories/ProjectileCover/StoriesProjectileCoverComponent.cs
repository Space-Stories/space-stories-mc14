using Robust.Shared.GameStates;

namespace Content.Shared._Stories.ProjectileCover;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StoriesProjectileCoverSystem))]
public sealed partial class StoriesProjectileCoverComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Coverage = 60;

    [DataField, AutoNetworkedField]
    public bool Directional;

    [DataField, AutoNetworkedField]
    public bool BlockThrown;

    [DataField]
    public string FixtureId = "fix1";
}
