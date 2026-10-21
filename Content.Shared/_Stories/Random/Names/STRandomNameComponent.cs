using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Random.Names;

[RegisterComponent, NetworkedComponent]
public sealed partial class STRandomNameComponent : Component
{
    [DataField(required: true)]
    public LocId BaseName;

    [DataField(required: true)]
    public LocId PostFix;

    [DataField]
    public int MaxNumber = 2500;
}
