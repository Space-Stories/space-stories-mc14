using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralRush;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STFeralRushSystem))]
public sealed partial class STFeralRushComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan SpeedDuration = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan ArmorDuration = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.3f;

    [DataField, AutoNetworkedField]
    public int ArmorGain = 10;

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Stories/Voice/Predalien/predalien_growl.ogg");

    [DataField, AutoNetworkedField]
    public Color RushColor = Color.FromHex("#A31010");

    [DataField, AutoNetworkedField]
    public EntProtoId RushEffect = "RMCEffectEmpowerTantrum";
}
