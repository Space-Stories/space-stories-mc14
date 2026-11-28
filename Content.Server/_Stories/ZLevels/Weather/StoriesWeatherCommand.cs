using System.Linq;
using Content.Server.Administration;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Weather;
using Content.Shared.Administration;
using Content.Shared.Prototypes;
using Content.Shared.Weather;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.ZLevels.Weather;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class StoriesWeatherCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    public override string Command => "znetwork-weather";
    public override string Description => "Sets weather for all maps in zNetwork";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmd-weather-error-no-arguments"));
            return;
        }

        // get the target
        EntityUid? target;

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out target))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.TryGetComponent<ZMapNetworkComponent>(target, out var levelComp))
        {
            shell.WriteError($"Target entity doesnt have ZMapNetworkComponent {args[0]}");
            return;
        }

        //Weather proto parse
        var weatherProto = (EntProtoId?) args[1];
        if (args[1] == "null")
            weatherProto = null;
        else if (!_proto.TryIndex(weatherProto, out _))
        {
            shell.WriteError(Loc.GetString("cmd-weather-error-unknown-proto"));
            return;
        }

        //Time parsing
        var duration = (TimeSpan?) null;
        if (args.Length == 3)
        {
            if (int.TryParse(args[2], out var durationInt))
            {
                duration = TimeSpan.FromSeconds(durationInt);
            }
            else
            {
                shell.WriteError(Loc.GetString("cmd-weather-error-wrong-time"));
            }
        }

        _entities.System<StoriesWeatherSystem>().SetWeather((target.Value, levelComp), weatherProto, duration);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = new List<CompletionOption>();
            var query = _entities.EntityQueryEnumerator<ZMapNetworkComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var meta))
            {
                options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
            }
            return CompletionResult.FromHintOptions(options, "zNetwork net entity");
        }

        if (args.Length == 2)
        {
            var options = CompletionHelper.PrototypeIDs<WeatherPrototype>(true, _proto);
            var withNull = options.Concat(new[] { new CompletionOption("null", Loc.GetString("cmd-weather-null")) });
            return CompletionResult.FromHintOptions(withNull, Loc.GetString("cmd-weather-hint"));
        }

        if (args.Length == 3)
        {
            return CompletionResult.FromHint("Duration in seconds");
        }

        return CompletionResult.Empty;
    }
}
