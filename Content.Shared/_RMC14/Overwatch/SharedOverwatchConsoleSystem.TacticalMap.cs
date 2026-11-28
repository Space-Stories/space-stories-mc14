using System.Linq;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Overwatch;

public abstract partial class SharedOverwatchConsoleSystem
{
    public bool TryWatchTarget(Entity<OverwatchConsoleComponent> ent, EntityUid actor, EntityUid target)
    {
        if (actor == target || TerminatingOrDeleted(target))
            return false;

        if (ent.Comp.Squad is not { } selectedSquad)
            return false;

        var selectedSquadNet = selectedSquad;
        var targetNet = GetNetEntity(target);
        var state = GetOverwatchData(ent.Comp);
        var validMarineCamera = state.Marines.TryGetValue(selectedSquadNet, out var selectedMarines) &&
                                selectedMarines.Any(marine => marine.Id == targetNet && marine.Camera != default);
        var validTripodCamera = state.Cameras.TryGetValue(selectedSquadNet, out var selectedCameras) &&
                                selectedCameras.Any(camera => camera.Id == targetNet);

        if (!validMarineCamera && !validTripodCamera)
            return false;

        Entity<OverwatchCameraComponent?> camera;
        if (!_inventory.TryGetInventoryEntity<OverwatchCameraComponent>(target, out camera))
        {
            if (!TryComp(target, out RMCOverwatchTripodCameraComponent? tripod) ||
                !tripod.Deployed ||
                tripod.Squad != GetEntity(selectedSquad) ||
                !TryComp(target, out OverwatchCameraComponent? tripodCamera))
            {
                return false;
            }

            camera = (target, tripodCamera);
        }

        if (HasComp<ScopingComponent>(actor))
        {
            if (_net.IsServer)
            {
                _popup.PopupCursor(Loc.GetString("rmc-overwatch-console-scoped"), actor, PopupType.MediumCaution);
            }
            return false;
        }

        if (_net.IsServer &&
            TryComp(actor, out OverwatchWatchingComponent? watching) &&
            watching.Watching == camera.Owner &&
            TryComp(actor, out ActorComponent? actorComp) &&
            TryComp(actor, out EyeComponent? eye))
        {
            Unwatch((actor, eye), actorComp.PlayerSession);
            return false;
        }

        Watch(actor, camera);
        return true;
    }
}
