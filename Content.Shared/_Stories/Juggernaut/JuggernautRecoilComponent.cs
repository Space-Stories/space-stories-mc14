using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>M134C-JLCW recoil profile layered on top of GunToggleableRecoil + GunSpinup - see JuggernautRecoilSystem.</summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(JuggernautRecoilSystem))]
public sealed partial class JuggernautRecoilComponent : Component
{
    [DataField]
    public float CompensatedMinAngleDegrees = 10;

    [DataField]
    public float CompensatedMaxAngleDegrees = 15;

    [DataField]
    public float UncompensatedKickScale = 0.5f;
}
