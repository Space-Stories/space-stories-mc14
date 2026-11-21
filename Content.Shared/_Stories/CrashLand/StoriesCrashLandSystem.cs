using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.ParaDrop;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Shuttles.Components;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Shared._Stories.CrashLand;

public sealed class StoriesCrashLandSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedCrashLandSystem _crashLand = default!;
    [Dependency] private readonly HardpointSystem _hardpoint = default!;
    [Dependency] private readonly INetManager _net = default!;

    private EntityQuery<CrashLandableComponent> _crashLandableQuery;
    private EntityQuery<CrashLandingComponent> _crashLandingQuery;
    private EntityQuery<FTLComponent> _ftlQuery;
    private EntityQuery<SkyFallingComponent> _skyFallingQuery;

    private readonly HashSet<EntityUid> _occupants = new();
    private readonly HashSet<EntityUid> _visitedHardpoints = new();
    private bool _crashLandEnabled;
    private float _checkTimer;

    private const float CheckInterval = 0.5f;

    public override void Initialize()
    {
        _crashLandableQuery = GetEntityQuery<CrashLandableComponent>();
        _crashLandingQuery = GetEntityQuery<CrashLandingComponent>();
        _ftlQuery = GetEntityQuery<FTLComponent>();
        _skyFallingQuery = GetEntityQuery<SkyFallingComponent>();

        SubscribeLocalEvent<VehicleComponent, CrashLandedEvent>(OnVehicleCrashLanded);

        Subs.CVar(_config, RMCCVars.RMCFTLCrashLand, v => _crashLandEnabled = v, true);
    }

    private void OnVehicleCrashLanded(Entity<VehicleComponent> ent, ref CrashLandedEvent args)
    {
        if (!args.ShouldDamage || _net.IsClient)
            return;

        if (TryComp(ent, out HardpointIntegrityComponent? frameIntegrity) && frameIntegrity.Integrity > 0f)
            _hardpoint.DamageHardpoint(ent.Owner, ent.Owner, frameIntegrity.Integrity, frameIntegrity);

        _visitedHardpoints.Clear();
        DamageInstalledHardpoints(ent.Owner, ent.Owner);

        _occupants.Clear();

        if (ent.Comp.Operator is { } operatorUid)
            _occupants.Add(operatorUid);

        if (TryComp(ent, out VehicleInteriorComponent? interior))
        {
            foreach (var passenger in interior.Passengers)
            {
                _occupants.Add(passenger);
            }

            foreach (var xeno in interior.Xenos)
            {
                _occupants.Add(xeno);
            }
        }

        if (TryComp(ent, out StrapComponent? strap))
        {
            foreach (var buckled in strap.BuckledEntities)
            {
                _occupants.Add(buckled);
            }
        }

        foreach (var occupant in _occupants)
        {
            if (TerminatingOrDeleted(occupant))
                continue;

            _crashLand.ApplyFallingDamage(occupant);
        }
    }

    private void DamageInstalledHardpoints(EntityUid vehicle, EntityUid containerEnt)
    {
        if (!TryComp(containerEnt, out ItemSlotsComponent? itemSlots))
            return;

        foreach (var slot in itemSlots.Slots.Values)
        {
            if (slot.Item is not { } item || !_visitedHardpoints.Add(item))
                continue;

            if (TryComp(item, out HardpointIntegrityComponent? integrity) && integrity.Integrity > 0f)
                _hardpoint.DamageHardpoint(vehicle, item, integrity.Integrity, integrity);

            DamageInstalledHardpoints(vehicle, item);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient || !_crashLandEnabled)
            return;

        _checkTimer += frameTime;
        if (_checkTimer < CheckInterval)
            return;

        _checkTimer = 0f;

        var ftlMaps = EntityQueryEnumerator<FTLMapComponent, TransformComponent>();
        while (ftlMaps.MoveNext(out _, out _, out var xform))
        {
            var children = xform.ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (!_crashLandableQuery.TryComp(child, out var crashLandable))
                    continue;

                if (_crashLandingQuery.HasComp(child) ||
                    _skyFallingQuery.HasComp(child) ||
                    _ftlQuery.HasComp(child))
                {
                    continue;
                }

                _crashLand.TryCrashLand((child, crashLandable), true);
            }
        }
    }
}
