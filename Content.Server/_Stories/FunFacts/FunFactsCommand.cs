using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stories.FunFacts;

[AdminCommand(AdminFlags.Debug)]
public sealed class FunFactsCommand : LocalizedEntityCommands
{
    [Dependency] private readonly FunFactsSystem _funFacts = default!;

    public override string Command => "funfacts";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var lines = _funFacts.GetDebugReport();
        if (lines.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-funfacts-empty"));
            return;
        }

        foreach (var line in lines)
        {
            shell.WriteLine(line);
        }
    }
}
