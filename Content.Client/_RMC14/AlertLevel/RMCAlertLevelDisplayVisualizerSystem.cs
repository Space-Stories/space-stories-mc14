using System.Numerics;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared.Clock;
using Content.Shared.GameTicking;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.AlertLevel;

public sealed class RMCAlertLevelDisplayVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly RMCAlertLevelSystem _alertLevel = default!;
    // Stories-ZLevels-Start

    private float _updateTimer;
    private const float UpdateInterval = 1.0f;
    private string? _lastTimeString;
    private RMCAlertLevels? _lastAlertLevel;
    private bool _forceUpdate = true;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCAlertLevelDisplayComponent, ComponentStartup>(OnDisplayStartup);
    }

    private void OnDisplayStartup(Entity<RMCAlertLevelDisplayComponent> ent, ref ComponentStartup args)
    {
        _forceUpdate = true;
    }
    // Stories-ZLevels-End

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Stories-ZLevels-Start
        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval && !_forceUpdate)
            return;

        _updateTimer = 0f;

        // Stories-ZLevels-End
        var current = _alertLevel.Get();
        // Stories-ZLevels-Start
        if (current > RMCAlertLevels.Green)
        {
            _lastAlertLevel = current;
            _forceUpdate = false;
            return;
        }
        // Stories-ZLevels-End

        // Stories-ZLevels-Start
        var timeOffset = TimeSpan.Zero;
        var timeQuery = EntityQueryEnumerator<GlobalTimeManagerComponent>();
        if (timeQuery.MoveNext(out _, out var timeComp))
        {
            timeOffset = timeComp.TimeOffset;
        }

        var worldTime = timeOffset + _ticker.RoundDuration();
        var timeString = worldTime.ToString(@"hh\:mm");

        if (!_forceUpdate && timeString == _lastTimeString && current == _lastAlertLevel)
        // Stories-ZLevels-End
            return;
        // Stories-ZLevels-Start

        _lastTimeString = timeString;
        _lastAlertLevel = current;
        _forceUpdate = false;

        var hourTensState = $"{timeString[0]}";
        var hourOnesState = $"{timeString[1]}";
        var separatorState = "~";
        var minuteTensState = $"{timeString[3]}";
        var minuteOnesState = $"{timeString[4]}";
        // Stories-ZLevels-End

        var query = EntityQueryEnumerator<RMCAlertLevelDisplayComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            if (!sprite.LayerMapTryGet(RMCAlertLevelDisplayVisualLayers.HourTens, out var hourTensLayer) ||
                !sprite.LayerMapTryGet(RMCAlertLevelDisplayVisualLayers.HourOnes, out var hourOnesLayer) ||
                !sprite.LayerMapTryGet(RMCAlertLevelDisplayVisualLayers.Separator, out var separatorLayer) ||
                !sprite.LayerMapTryGet(RMCAlertLevelDisplayVisualLayers.MinuteTens, out var minuteTensLayer) ||
                // Stories-ZLevels-Start
                !sprite.LayerMapTryGet(RMCAlertLevelDisplayVisualLayers.MinuteOnes, out var minuteOnesLayer))
            {
                // Stories-ZLevels-End
                continue;
            // Stories-ZLevels-Start
            }
            // Stories-ZLevels-End

            sprite.LayerSetOffset(hourTensLayer, new Vector2(0.11f, -0.4375f));
            sprite.LayerSetOffset(hourOnesLayer, new Vector2(0.28f, -0.4375f));
            sprite.LayerSetOffset(separatorLayer, new Vector2(0.406f, -0.4375f));
            sprite.LayerSetOffset(minuteTensLayer, new Vector2(0.56f, -0.4375f));
            sprite.LayerSetOffset(minuteOnesLayer, new Vector2(0.73f, -0.4375f));

            sprite.LayerSetState(hourTensLayer, hourTensState);
            sprite.LayerSetState(hourOnesLayer, hourOnesState);
            sprite.LayerSetState(separatorLayer, separatorState);
            sprite.LayerSetState(minuteTensLayer, minuteTensState);
            sprite.LayerSetState(minuteOnesLayer, minuteOnesState);
        }
    }
}
