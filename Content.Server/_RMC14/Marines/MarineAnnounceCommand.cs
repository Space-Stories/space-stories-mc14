using System.Linq;
using Content.Server.Administration;
using Content.Shared._Stories.TTS;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Marines;

[AdminCommand(AdminFlags.Fun)]
public sealed class MarineAnnounceCommand : IConsoleCommand
{
    public string Command => "marineannounce";
    public string Description => Loc.GetString("rmc-command-marineannounce-description");
    public string Help => "Usage: marineannounce <voiceId|default> <author> <message...>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var marineAnnounce = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<MarineAnnounceSystem>();
        if (args.Length < 3)
        {
            shell.WriteError("Not enough arguments! Need at least 3.");
            return;
        }

        var voiceId = args[0] == "default" ? null : args[0];
        var author = args[1].Replace("_", " ");
        var message = string.Join(' ', args[2..]);

        marineAnnounce.AnnounceHighCommand(message, author, voiceId: voiceId);
        shell.WriteLine("Sent!");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var proto = IoCManager.Resolve<IPrototypeManager>();
            var voices = proto.EnumeratePrototypes<TTSVoicePrototype>().Select(v => v.ID).ToList();
            voices.Add("default");
            return CompletionResult.FromHintOptions(voices, "Voice ID or 'default'");
        }

        if (args.Length == 2)
            return CompletionResult.FromHint("Author");

        return CompletionResult.FromHint("Message");
    }
}
