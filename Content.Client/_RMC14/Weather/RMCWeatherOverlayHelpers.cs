using System.Numerics;
using Content.Client.Weather;
using Content.Shared._RMC14.Weather;
using Content.Shared.Weather;
using Robust.Shared.Map;

namespace Content.Client._RMC14.Weather;

public static class RMCWeatherOverlayHelpers
{
    public static (Vector2 ClearHalfSize, Vector2 FullHalfSize) GetOverlayHalfSizes(
        RMCWeatherScreenOverlay overlay,
        float width,
        float height)
    {
        if (overlay == RMCWeatherScreenOverlay.None)
            return default;

        var referenceHalf = MathF.Min(width, height) / 2f;
        var radii = RMCWeatherScreenOverlayData.GetRadii(overlay);
        var fullscreenTiles = RMCWeatherScreenOverlayData.FullscreenTiles;
        var viewportHalf = new Vector2(width, height) / 2f;

        return (new Vector2(
                referenceHalf * radii.ClearX / fullscreenTiles,
                referenceHalf * radii.ClearY / fullscreenTiles),
            new Vector2(
                viewportHalf.X * radii.FullX / fullscreenTiles,
                viewportHalf.Y * radii.FullY / fullscreenTiles));
    }

    public static bool TryGetCurrentMapOverlay(IEntityManager entity, MapId mapId, out RMCWeatherOverlayContext context)
    {
        context = default;
        var overlay = RMCWeatherScreenOverlay.None;
        var zLevels = entity.System<Content.Shared._Stories.ZLevels.Core.EntitySystems.SharedZLevelsSystem>();
        var mapEnt = entity.System<SharedMapSystem>().GetMapOrInvalid(mapId);

        var query = entity.EntityQueryEnumerator<RMCWeatherCycleComponent, TransformComponent>();
        while (query.MoveNext(out _, out var cycle, out var xform))
        {
            // Stories-ZLevels-Start
            if (cycle.State != RMCWeatherCycleState.Running ||
                cycle.CurrentScreenOverlay == RMCWeatherScreenOverlay.None ||
                xform.MapUid is not { } cycleMapUid)
            // Stories-ZLevels-End
            {
                continue;
            }
            // Stories-ZLevels-Start

            if (xform.MapID != mapId && !IsSameMapOrZNetwork(zLevels, mapEnt, cycleMapUid))
                continue;
            // Stories-ZLevels-End

            if ((byte) cycle.CurrentScreenOverlay <= (byte) overlay)
                continue;

            overlay = cycle.CurrentScreenOverlay;
        }

        if (overlay == RMCWeatherScreenOverlay.None)
            return false;

        context = new RMCWeatherOverlayContext(overlay);
        return true;
    }

    private static bool IsSameMapOrZNetwork(Content.Shared._Stories.ZLevels.Core.EntitySystems.SharedZLevelsSystem zLevels, EntityUid map1, EntityUid map2)
    {
        if (map1 == map2)
            return true;

        if (zLevels.TryGetMapNetwork(map1, out var net1) &&
            zLevels.TryGetMapNetwork(map2, out var net2))
        {
            return net1.Owner == net2.Owner;
        }

        return false;
    }

    public static RMCWeatherScreenOverlay GetCurrentMapOverlay(IEntityManager entity, MapId mapId)
    {
        return TryGetCurrentMapOverlay(entity, mapId, out var context)
            ? context.Overlay
            : RMCWeatherScreenOverlay.None;
    }

    public static float GetWeatherAlpha(IEntityManager entity, WeatherSystem weatherSystem, EntityUid mapUid)
    {
        if (!entity.TryGetComponent(mapUid, out WeatherComponent? weather))
            return 0f;

        var alpha = 0f;
        foreach (var data in weather.Weather.Values)
        {
            alpha = MathF.Max(alpha, weatherSystem.GetPercent(data, mapUid));
        }

        return Math.Clamp(alpha, 0f, 1f);
    }
}

public readonly record struct RMCWeatherOverlayContext(RMCWeatherScreenOverlay Overlay);
