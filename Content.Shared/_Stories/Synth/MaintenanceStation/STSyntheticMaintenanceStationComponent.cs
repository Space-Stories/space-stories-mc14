using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class STSyntheticMaintenanceStationComponent : Component
{
    public const string BodyContainerId = "st_synthetic_maintenance_station_body";

    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    [DataField, AutoNetworkedField]
    public bool Occupied;

    [DataField]
    public float MaxInternalCharge = 15000;

    [DataField]
    public float CurrentInternalCharge = 15000;

    [DataField]
    public float PassiveRechargeRate = 2500;

    [DataField]
    public float ActiveRechargeRate = 25000;

    [DataField]
    public float UnpoweredDrainRate = 50;

    [DataField]
    public float RepairChargeCost = 500;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan InsertDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ExitStun = TimeSpan.FromSeconds(2);

    [DataField]
    public DamageSpecifier RepairDamage = new()
    {
        DamageDict =
        {
            ["Blunt"] = -10,
            ["Piercing"] = -10,
            ["Slash"] = -10,
            ["Heat"] = -10,
            ["Shock"] = -10,
            ["Cold"] = -10,
            ["Caustic"] = -10,
        },
    };

    [DataField]
    public FixedPoint2 BloodRestoreAmount = 10;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}

[Serializable, NetSerializable]
public enum STSyntheticMaintenanceStationVisuals
{
    Status,
    Charge,
}

[Serializable, NetSerializable]
public enum STSyntheticMaintenanceStationLayers
{
    Base,
    Charge,
}

[Serializable, NetSerializable]
public enum STSyntheticMaintenanceStationStatus
{
    Off,
    Empty,
    Occupied,
}

[Serializable, NetSerializable]
public enum STSyntheticMaintenanceStationCharge
{
    Empty,
    Low,
    Medium,
    High,
    Full,
}

[Serializable, NetSerializable]
public sealed partial class STSyntheticMaintenanceStationInsertDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity Inserted;

    public STSyntheticMaintenanceStationInsertDoAfterEvent()
    {
    }

    public STSyntheticMaintenanceStationInsertDoAfterEvent(NetEntity inserted)
    {
        Inserted = inserted;
    }
}
