using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Ordnance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedOrdnanceCasingSystem))]
public sealed partial class OrdnanceCasingComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsLocked;

    [DataField, AutoNetworkedField]
    public bool BlastDampener;

    [DataField, AutoNetworkedField]
    public bool HasBlastDampener = true;

    [DataField, AutoNetworkedField]
    public SkillWhitelist? DampenerSkills;

    [DataField, AutoNetworkedField]
    public string TriggerSlotId = "ordnance_trigger_slot";

    [DataField, AutoNetworkedField]
    public string BeakerSlot1Id = "beaker_1";

    [DataField, AutoNetworkedField]
    public string BeakerSlot2Id = "beaker_2";

    [DataField, AutoNetworkedField]
    public string FuelSlotId = "fuel_slot";

    [DataField, AutoNetworkedField]
    public string WarheadSlotId = "warhead_slot";

    [DataField, AutoNetworkedField]
    public string BallisticAmmoSlotId = "ballistic-ammo";

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxVolume = 1000;

    [DataField, AutoNetworkedField]
    public string RequiredAssemblyMode = "Any";

    [DataField, AutoNetworkedField]
    public float? DualIgniterConeAngle;

    [DataField]
    public ComponentRegistry AddedComponents = new();

    [DataField]
    public List<string> AddedTags = new();

    [DataField, AutoNetworkedField]
    public float BaseFalloff = 75f;

    [DataField, AutoNetworkedField]
    public float MinFalloff = 25f;

    [DataField, AutoNetworkedField]
    public float MaxExplosionPower = 175f;

    [DataField, AutoNetworkedField]
    public int MaxShards = 8;

    [DataField, AutoNetworkedField]
    public float MaxFireRadius = 5f;

    [DataField, AutoNetworkedField]
    public float MaxFireIntensity = 20f;

    [DataField, AutoNetworkedField]
    public float MaxFireDuration = 24f;

    [DataField, AutoNetworkedField]
    public float MinFireRadius = 1f;

    [DataField, AutoNetworkedField]
    public float MinFireIntensity = 3f;

    [DataField, AutoNetworkedField]
    public float MinFireDuration = 3f;

    [DataField, AutoNetworkedField]
    public bool UseDirection;

    [DataField, AutoNetworkedField]
    public float ConeAngle = 60f;

    [DataField, AutoNetworkedField]
    public EntProtoId ShrapnelProto = "STProjectileOrdnanceShrapnel";

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype>? RequiredFuelReagent;

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> IronReagent = "RMCIron";

    [DataField, AutoNetworkedField]
    public FixedPoint2 RequiredFuelAmount = 60;

    [DataField, AutoNetworkedField]
    public float SignallerDelay = 0f;

    [DataField, AutoNetworkedField]
    public bool UpdateAppearance = true;

    [DataField, AutoNetworkedField]
    public bool IsAssembly = true;

    [DataField, AutoNetworkedField]
    public bool AllowStarShape = true;

    [DataField, AutoNetworkedField]
    public float WarheadMaxRange = 6f;
}
