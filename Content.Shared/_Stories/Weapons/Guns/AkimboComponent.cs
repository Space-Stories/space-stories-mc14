using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Weapons.Guns;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AkimboComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 AccuracyMultiplier = 1;

    [DataField, AutoNetworkedField]
    public float ScatterMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float IndividualFireRateMultiplier = 0.625f;

    [DataField, AutoNetworkedField]
    public float TotalFireRateMultiplier = 1.25f;

    [DataField, AutoNetworkedField]
    public float RecoilModifier = 0.5f;
}
