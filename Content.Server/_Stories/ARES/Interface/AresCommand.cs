using Content.Server._Stories.ARES.Administration;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stories.ARES.Interface;

[AdminCommand(AdminFlags.Fun)]
public sealed class AresCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public override string Command => "ares";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } admin)
            return;
        _entities.System<AresAdminSystem>().Open(admin);
    }
}
