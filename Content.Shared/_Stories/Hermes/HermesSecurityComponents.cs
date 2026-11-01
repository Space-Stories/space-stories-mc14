using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Hermes;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedHermesSystem))]
public sealed partial class HermesSecurityVentComponent : Component
{
    [DataField] public EntProtoId GasPrototype = "STGasCN20X";
    [DataField] public TimeSpan WarningTime = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    [DataField] public string? Label;
    [DataField] public SoundSpecifier ReleaseSound = new SoundPathSpecifier("/Audio/Effects/smoke.ogg");
    [DataField] public SoundSpecifier WarningSound = new SoundPathSpecifier("/Audio/Items/hiss.ogg");
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ReadyAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ReleaseAt;
    public EntityUid? RequestedBy;
    public EntityUid? Core;
    public bool LastReady;
}

[RegisterComponent]
public sealed partial class HermesLockdownDoorComponent : Component
{
    public bool Locked;
}

[RegisterComponent]
public sealed partial class HermesProcessorComponent : Component;

[RegisterComponent]
public sealed partial class HermesSentryComponent : Component
{
    public bool LockedDown;
    public NetEntity? AppliedEntity;
    public HermesIff? AppliedIff;
}
