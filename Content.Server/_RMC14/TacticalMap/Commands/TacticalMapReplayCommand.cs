using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server._RMC14.TacticalMap;

[AnyCommand]
public sealed class TacticalMapReplayCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public string Command => "tacmapreplay";
    public string Description => Loc.GetString("rmc-tactical-map-replay-description");
    public string Help => Loc.GetString("rmc-tactical-map-replay-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine(Loc.GetString("rmc-tactical-map-replay-player-only"));
            return;
        }

        var replay = _entitySystemManager.GetEntitySystem<TacticalMapReplaySystem>();
        if (!replay.CanAccessReplay(player, out var reason))
        {
            shell.WriteLine(reason ?? Loc.GetString("rmc-tactical-map-replay-no-access"));
            return;
        }

        string? mapId = args.Length == 1 ? args[0] : null;
        replay.SendReplay(player, mapId);
    }
}
