using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Ordnance.Simulator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DemolitionsSimulatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextDetonationTime;

    [DataField]
    public TimeSpan ResetCooldown = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextResetTime;

    [DataField, AutoNetworkedField]
    public bool IsSimulating;

    [DataField]
    public string ItemSlotId = "demo_sim_slot";

    [DataField, AutoNetworkedField]
    public SkillWhitelist? RequiredSkills;

    [DataField, AutoNetworkedField]
    public string SelectedCategory = "Xeno";

    [DataField(required: true), AutoNetworkedField]
    public Dictionary<string, List<EntProtoId>> SpawnCategories = new();

    [DataField, AutoNetworkedField]
    public EntProtoId SelectedPrototype = "CMXenoDrone";

    [DataField]
    public ResPath ArenaMapPath = new("/Maps/Test/admin_test_arena.yml");

    [DataField, AutoNetworkedField]
    public NetEntity? ArenaMap;

    [DataField, AutoNetworkedField]
    public NetEntity? ArenaGrid;

    [DataField, AutoNetworkedField]
    public NetEntity? Camera;

    [DataField, AutoNetworkedField]
    public float CleanupRadius = 15f;

    [DataField]
    public EntProtoId CameraPrototype = "STDemolitionsSimulatorCamera";

    [DataField]
    public TimeSpan SimulationDelay = TimeSpan.FromSeconds(1.5);
}
