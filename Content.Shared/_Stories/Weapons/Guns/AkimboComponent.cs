using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Weapons.Guns;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AkimboComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 AccuracyMultiplier = 1.5;

    [DataField, AutoNetworkedField]
    public float ScatterMultiplier = 1.5f;

    [DataField, AutoNetworkedField]
    public float IndividualFireRateMultiplier = 1.0f;

    [DataField, AutoNetworkedField]
    public float TotalFireRateMultiplier = 2.0f;

    [DataField, AutoNetworkedField]
    public float RecoilModifier = 1.0f;
}
