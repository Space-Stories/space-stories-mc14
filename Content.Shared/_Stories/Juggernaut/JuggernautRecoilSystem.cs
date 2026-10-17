using System.Numerics;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Recoil;
using Content.Shared.Camera;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Juggernaut;

public sealed class JuggernautRecoilSystem : EntitySystem
{
    [Dependency] private readonly SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        // After both RMC handlers so this overrides GunToggleableRecoil's zeroed scatter without disturbing GunSpinup's.
        SubscribeLocalEvent<JuggernautRecoilComponent, GunRefreshModifiersEvent>(OnRefreshModifiers,
            after: [typeof(GunToggleableRecoilSystem), typeof(GunSpinupSystem)]);
        SubscribeLocalEvent<JuggernautRecoilComponent, GunShotEvent>(OnShot);
    }

    private void OnRefreshModifiers(Entity<JuggernautRecoilComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!IsCompensating(ent))
            return;

        args.MinAngle = Angle.FromDegrees(ent.Comp.CompensatedMinAngleDegrees);
        args.MaxAngle = Angle.FromDegrees(ent.Comp.CompensatedMaxAngleDegrees);
    }

    private void OnShot(Entity<JuggernautRecoilComponent> ent, ref GunShotEvent args)
    {
        if (IsCompensating(ent))
            return;

        // Uncompensated scatter is wide enough that the engine's post-scatter kick averages to nothing.
        if (_net.IsServer || !_timing.IsFirstTimePredicted)
            return;

        if (!TryComp(ent, out GunComponent? gun) || gun.CameraRecoilScalarModified == 0)
            return;

        var from = _transform.ToMapCoordinates(args.FromCoordinates).Position;
        var to = _transform.ToMapCoordinates(args.ToCoordinates).Position;
        var direction = to - from;
        if (direction == Vector2.Zero)
            return;

        _cameraRecoil.KickCamera(args.User, direction.Normalized() * ent.Comp.UncompensatedKickScale * gun.CameraRecoilScalarModified);
    }

    private bool IsCompensating(EntityUid gun)
    {
        return TryComp(gun, out GunToggleableRecoilComponent? recoil) && recoil.Active;
    }
}
