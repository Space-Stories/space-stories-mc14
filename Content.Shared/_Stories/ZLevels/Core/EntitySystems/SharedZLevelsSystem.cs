using System.Diagnostics.CodeAnalysis;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] protected readonly Content.Shared.Alert.AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly Content.Shared.Light.EntitySystems.SharedRoofSystem _roof = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.Alert.AlertPrototype> LookUpAlert = "STZLayerLookUp";

    protected EntityQuery<ZPhysicsComponent> ZPhysicsQuery;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<Content.Shared.Light.Components.RoofComponent> _roofQuery;

    private EntityQuery<ZMapComponent> _zMapQuery;
    private EntityQuery<ZMapNetworkComponent> _zNetworkQuery;
    private EntityQuery<ZLevelHighGroundComponent> _zHighGroundQuery;
    private EntityQuery<Content.Shared._Stories.ZLevels.Ladder.ZLevelLadderComponent> _zLadderQuery;
    private EntityQuery<Content.Shared._RMC14.Ladder.LadderComponent> _rmcLadderQuery;

    private readonly HashSet<EntityUid> _ladderLookupEntities = new();

    private bool _clientSimulation;
    private TimeSpan _fixedTimestep;

    public override void Initialize()
    {
        base.Initialize();

        ZPhysicsQuery = GetEntityQuery<ZPhysicsComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        _roofQuery = GetEntityQuery<Content.Shared.Light.Components.RoofComponent>();
        _zMapQuery = GetEntityQuery<ZMapComponent>();
        _zNetworkQuery = GetEntityQuery<ZMapNetworkComponent>();
        _zHighGroundQuery = GetEntityQuery<ZLevelHighGroundComponent>();
        _zLadderQuery = GetEntityQuery<Content.Shared._Stories.ZLevels.Ladder.ZLevelLadderComponent>();
        _rmcLadderQuery = GetEntityQuery<Content.Shared._RMC14.Ladder.LadderComponent>();
        _zGridQuery = GetEntityQuery<ZGridComponent>();
        _zGridNetworkQuery = GetEntityQuery<ZGridNetworkComponent>();

        _config.OnValueChanged(CCVars.ZLevelsPhysicsClientSimulation, i => _clientSimulation = i, true);
        _config.OnValueChanged(CCVars.ZLevelsPhysicsTickRate, i => _fixedTimestep = TimeSpan.FromSeconds(1d / i), true);

        InitializeActivation();
        InitializeCacheHooks();
        InitializeMovement();
        InitializeView();
    }
}
