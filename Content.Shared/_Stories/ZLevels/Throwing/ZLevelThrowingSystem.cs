using System.Numerics;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Stories.ZLevels.Throwing;

public sealed partial class ZLevelThrowingSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrownEvent>(OnItemThrown);
    }

    private void OnItemThrown(ref ThrownEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.User is not { } user)
            return;

        if (!TryComp<ZLevelViewerComponent>(user, out var viewer))
            return;

        var userXform = Transform(user);
        if (userXform.MapUid is not { } mapUid)
            return;

        // If user is actively looking up to upper Z-level
        if (viewer.LookUp)
        {
            var worldPos = _transform.GetWorldPosition(userXform);
            // Check edge occlusion: verify no opaque roof tile directly above thrower
            if (!_zLevels.HasOpaqueAbove(worldPos, mapUid))
            {
                _zLevels.TryMoveUp(args.Thrown);
            }
        }
        else if (_zLevels.TryMapOffset(mapUid, -1, out var mapBelow))
        {
            // If user is throwing down over an opening / pit tile
            var userWorldPos = _transform.GetWorldPosition(userXform);
            var targetPos = userWorldPos;
            if (TryComp<PhysicsComponent>(args.Thrown, out var physics) && physics.LinearVelocity != Vector2.Zero)
            {
                targetPos = userWorldPos + physics.LinearVelocity.Normalized() * 5f;
            }

            if (_zLevels.TryFindZShotOpening(mapUid, mapBelow, -1, userWorldPos, targetPos, out _))
            {
                _zLevels.TryMoveDown(args.Thrown);
            }
        }
    }
}
