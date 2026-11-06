using Content.Server._Stories.ZLevels.Core;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Rules;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Stories.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public class PlanetMappingZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ZLevelsSystem _zLevel = default!;

    public override string Command => "znetwork-planet-mapping";
    public override string Description => "Load existed colony planet map prototype as ZNetwork for mapping";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var options = new List<CompletionOption>();
        foreach (var entity in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!entity.TryGetComponent(out RMCPlanetMapPrototypeComponent? planetComp, _compFactory))
                continue;

            options.Add(new CompletionOption(entity.ID, string.IsNullOrEmpty(entity.Name) ? entity.ID : entity.Name));
        }

        options.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase));
        return CompletionResult.FromHintOptions(options, "PlanetEntityPrototype");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError("Wrong arguments count.");
            return;
        }

        // Get Entity Prototype
        if (!_proto.TryIndex<EntityPrototype>(args[0], out var planetProto))
        {
            shell.WriteError($"Unknown EntityPrototype {args[0]}");
            return;
        }

        if (!planetProto.TryGetComponent(out RMCPlanetMapPrototypeComponent? planetComp, _compFactory))
        {
            shell.WriteError($"EntityPrototype {planetProto.ID} does not have {nameof(RMCPlanetMapPrototypeComponent)}");
            return;
        }

        var planetName = string.IsNullOrEmpty(planetProto.Name) ? planetProto.ID : planetProto.Name;

        var network = _zLevel.CreateMapNetwork();
        _meta.SetEntityName(network, $"Mapping zNetwork: {planetName}");
        var dict = new Dictionary<EntityUid, int>();
        var createdMaps = new List<MapId>();
        var opts = new DeserializationOptions { StoreYamlUids = true };

        // Load default map
        if (!_mapLoader.TryLoadMap(planetComp.Map, out var defaultMapEnt, out _, opts) || defaultMapEnt == null)
        {
            shell.WriteError($"Failed to load default zNetwork map: {planetComp.Map}!");
            EntityManager.QueueDeleteEntity(network);
            return;
        }

        dict.Add(defaultMapEnt.Value, 0);
        createdMaps.Add(defaultMapEnt.Value.Comp.MapId);
        _meta.SetEntityName(defaultMapEnt.Value, $"Mapping {planetName}");

        var loadFailed = false;

        // Loading maps below first
        var depth = planetComp.MapsBelow.Count * -1;
        foreach (var path in planetComp.MapsBelow)
        {
            if (!_mapLoader.TryLoadMap(path, out var mapEnt, out _, opts) || mapEnt == null)
            {
                shell.WriteError($"Failed to load zNetwork map (depth {depth}): {path}!");
                loadFailed = true;
                break;
            }

            dict.Add(mapEnt.Value, depth);
            createdMaps.Add(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"Mapping {planetName} [{depth}]");
            depth++;
        }

        if (!loadFailed)
        {
            depth = 1;
            foreach (var path in planetComp.MapsAbove)
            {
                if (!_mapLoader.TryLoadMap(path, out var mapEnt, out _, opts) || mapEnt == null)
                {
                    shell.WriteError($"Failed to load zNetwork map (depth {depth}): {path}!");
                    loadFailed = true;
                    break;
                }

                dict.Add(mapEnt.Value, depth);
                createdMaps.Add(mapEnt.Value.Comp.MapId);
                _meta.SetEntityName(mapEnt.Value, $"Mapping {planetName} [{depth}]");
                depth++;
            }
        }

        if (loadFailed)
        {
            shell.WriteError("Unloading all created maps...");
            foreach (var mapId in createdMaps)
            {
                _map.DeleteMap(mapId);
            }
            EntityManager.QueueDeleteEntity(network);
            return;
        }

        // Was the maps actually created or did it fail somehow?
        var success = true;
        foreach (var mapId in createdMaps)
        {
            if (!_map.MapExists(mapId))
            {
                success = false;
                shell.WriteError($"For some reason some maps dont exist after loading! MapId: {mapId}");
            }
        }

        if (!success || !_zLevel.TryAddMapsIntoNetwork(network, dict))
        {
            shell.WriteError("Failed to create zNetwork from loaded maps! Unloading all created maps...");
            foreach (var mapId in createdMaps)
            {
                _map.DeleteMap(mapId);
            }
            EntityManager.QueueDeleteEntity(network);
            return;
        }

        // Maps successfully created. Run misc helpful mapping commands
        if (player.AttachedEntity is { Valid: true } playerEntity &&
            (EntityManager.GetComponent<MetaDataComponent>(playerEntity).EntityPrototype is not { } proto || proto != GameTicker.AdminObserverPrototypeName))
        {
            shell.ExecuteCommand("aghost");
        }

        // Don't interrupt mapping with events or auto-shuttle
        shell.ExecuteCommand("changecvar events.enabled false");
        shell.ExecuteCommand("changecvar shuttle.auto_call_time 0");

        shell.ExecuteCommand($"tp 0 0 {defaultMapEnt.Value.Comp.MapId}");
        shell.RemoteExecuteCommand("mappingclientsidesetup");
        foreach (var mapId in createdMaps)
        {
            DebugTools.Assert(_map.IsPaused(mapId));
        }
    }
}

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class ColonyMappingZNetworkCommand : PlanetMappingZNetworkCommand
{
    public override string Command => "znetwork-colony-mapping";
    public override string Description => "Load existed colony planet map prototype as ZNetwork for mapping";
}
