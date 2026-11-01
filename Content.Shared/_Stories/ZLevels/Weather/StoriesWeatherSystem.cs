using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.ZLevels.Weather;

/// <summary>
/// A subsystem that connects WeatherSystem with ZLevelSystem. Allows you to control the weather for the entire z-network at once.
/// </summary>
public sealed partial class StoriesWeatherSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;

    public void SetWeather(Entity<ZMapNetworkComponent?> network, EntProtoId? proto, TimeSpan? duration)
    {
        if (!Resolve(network, ref network.Comp))
            return;

        var weatherProto = (WeatherPrototype?) null;
        if (proto != null && _proto.TryIndex<WeatherPrototype>(proto.Value.Id, out var indexedProto))
        {
            weatherProto = indexedProto;
        }

        var endTime = duration != null ? _timing.CurTime + duration.Value : (TimeSpan?) null;

        foreach (var (_, map) in network.Comp.ZLevels)
        {
            if (!TryComp<MapComponent>(map, out var mapComp))
                continue;

            _weather.SetWeather(mapComp.MapId, weatherProto, endTime);
        }
    }
}
