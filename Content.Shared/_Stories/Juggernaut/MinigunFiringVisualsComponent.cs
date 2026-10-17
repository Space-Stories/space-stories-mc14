using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Marks a gun whose wielded sprite should swap to an animated "-firing" RSI state while shooting - see <see cref="MinigunFiringSystem"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(MinigunFiringSystem))]
public sealed partial class MinigunFiringVisualsComponent : Component
{
    /// <summary>How long after the last shot to keep showing the firing animation - covers the gap between autofire shots.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan GraceAfterShot = TimeSpan.FromSeconds(0.3);

    [AutoNetworkedField]
    public TimeSpan? LastShotAt;
}

[Serializable, NetSerializable]
public enum MinigunFiringVisuals : byte
{
    Firing,
}
