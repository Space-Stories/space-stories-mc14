using Content.Shared._Stories.Xenonids.Despoiler;
using Content.Shared.ActionBlocker;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerBarrageInputSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private EntityQuery<XenoDespoilerArmedBarrageComponent> _armedQuery;
    private EntityQuery<XenoDespoilerChargingBarrageComponent> _chargingQuery;

    private bool _useHeld;
    private bool _secondaryHeld;

    public override void Initialize()
    {
        _armedQuery = GetEntityQuery<XenoDespoilerArmedBarrageComponent>();
        _chargingQuery = GetEntityQuery<XenoDespoilerChargingBarrageComponent>();

        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_player.LocalEntity is not { } ent ||
            !(_armedQuery.HasComp(ent) || _chargingQuery.HasComp(ent)))
        {
            _useHeld = false;
            _secondaryHeld = false;
            return;
        }

        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use) == BoundKeyState.Down;
        var secondaryDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;

        if (secondaryDown && !_secondaryHeld)
        {
            RaiseNetworkEvent(new XenoDespoilerBarrageCancelRequest());
        }
        else if (useDown && !_useHeld && !_chargingQuery.HasComp(ent))
        {
            if (_actionBlocker.CanConsciouslyPerformAction(ent) && TryGetCursorCoords(out var coords))
                RaiseNetworkEvent(new XenoDespoilerBarrageStartChargeRequest(GetNetCoordinates(coords)));
        }
        else if (!useDown && _useHeld && _chargingQuery.HasComp(ent))
        {
            if (TryGetCursorCoords(out var coords))
                RaiseNetworkEvent(new XenoDespoilerBarrageFireRequest(GetNetCoordinates(coords)));
        }

        _useHeld = useDown;
        _secondaryHeld = secondaryDown;
    }

    private bool TryGetCursorCoords(out EntityCoordinates coords)
    {
        coords = default;
        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);

        EntityUid grid;
        if (_mapManager.TryFindGridAt(mousePos, out var gridUid, out _))
            grid = gridUid;
        else if (_map.TryGetMap(mousePos.MapId, out var map))
            grid = map.Value;
        else
            return false;

        coords = _transform.ToCoordinates(grid, mousePos);
        return true;
    }
}
