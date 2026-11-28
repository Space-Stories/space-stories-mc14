using System;
using System.Numerics;
using Content.Client.Outline;
using Content.Shared._RMC14.Sprite;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;

namespace Content.Client._RMC14.Sprite;

public sealed class RMCSpriteVisualizerSystem : VisualizerSystem<SpriteSetRenderOrderComponent>
{
    // Stories-LightOffset-Start
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    // Stories-LightOffset-End

    // Stories-LightOffset-Start
    private static readonly Vector2 OffsetSouth = new(0.45f, -0.32f);
    private static readonly Vector2 OffsetEast = new(0.7f, -1.45f);
    private static readonly Vector2 OffsetNorth = new(-0.5f, -1.5f);
    private static readonly Vector2 OffsetWest = new(-0.7f, -0.4f);

    private static bool IsWallOffset(Vector2 offset)
    {
        return offset.EqualsApprox(OffsetSouth, 0.05f) ||
               offset.EqualsApprox(OffsetEast, 0.05f) ||
               offset.EqualsApprox(OffsetNorth, 0.05f) ||
               offset.EqualsApprox(OffsetWest, 0.05f);
    }

    private static Vector2 GetWallOffset(Direction dir) => dir switch
    {
        Direction.South => OffsetSouth,
        Direction.East => OffsetEast,
        Direction.North => OffsetNorth,
        Direction.West => OffsetWest,
        _ => OffsetSouth,
    };

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(InteractionOutlineSystem));
        _placement.DirectionChanged += OnPlacementDirectionChanged;
        _placement.PlacementChanged += OnPlacementChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _placement.DirectionChanged -= OnPlacementDirectionChanged;
        _placement.PlacementChanged -= OnPlacementChanged;
    }

    private EntityUid? GetPlacementGhost()
    {
        return (_placement as PlacementManager)?.CurrentPlacementOverlayEntity;
    }

    private void OnPlacementDirectionChanged(object? sender, EventArgs e)
    {
        UpdatePlacementGhost();
    }

    private void OnPlacementChanged(object? sender, EventArgs e)
    {
        UpdatePlacementGhost();
    }

    private void UpdatePlacementGhost()
    {
        if (!_placement.IsActive || GetPlacementGhost() is not { } ghost)
            return;

        if (TryComp<TransformComponent>(ghost, out var xform))
        {
            _transform.SetLocalRotation(ghost, _placement.Direction.ToAngle(), xform);
        }

        if (TryComp<SpriteSetRenderOrderComponent>(ghost, out var set) &&
            TryComp<SpriteComponent>(ghost, out var sprite))
        {
            var eyeRot = _eye.CurrentEye?.Rotation ?? Angle.Zero;
            var offset = set.Offset ?? Vector2.Zero;
            if (IsWallOffset(offset))
            {
                var visualDir = (_placement.Direction.ToAngle() + eyeRot).GetCardinalDir();
                offset = GetWallOffset(visualDir);
            }
            else if (sprite.NoRotation && eyeRot != Angle.Zero)
            {
                offset = (-eyeRot).RotateVec(offset);
            }

            _sprite.SetOffset((ghost, (SpriteComponent?) sprite), offset);
        }
    }
    // Stories-LightOffset-End

    protected override void OnAppearanceChange(EntityUid uid, SpriteSetRenderOrderComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData(uid, SpriteSetRenderOrderComponent.Appearance.Key, out int order, args.Component))
            args.Sprite.RenderOrder = (uint) order;

        if (AppearanceSystem.TryGetData(uid, SpriteSetRenderOrderComponent.Appearance.Offset, out Vector2 offset, args.Component))
        {
            var eyeRot = _eye.CurrentEye?.Rotation ?? Angle.Zero;
            var isPlacementGhost = _placement.IsActive && GetPlacementGhost() == uid;
            var rot = isPlacementGhost
                ? _placement.Direction.ToAngle()
                : (TryComp<TransformComponent>(uid, out var xform) ? xform.LocalRotation : Angle.Zero);

            if (IsWallOffset(offset))
            {
                var visualDir = (rot + eyeRot).GetCardinalDir();
                offset = GetWallOffset(visualDir);
            }
            else if (args.Sprite.NoRotation && eyeRot != Angle.Zero)
            {
                offset = (-eyeRot).RotateVec(offset);
            }

            _sprite.SetOffset((uid, args.Sprite), offset);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        var eyeRot = _eye.CurrentEye?.Rotation ?? Angle.Zero;
        var query = AllEntityQuery<SpriteSetRenderOrderComponent, SpriteComponent, TransformComponent>();
        var ghostUid = _placement.IsActive ? GetPlacementGhost() : null;

        while (query.MoveNext(out var uid, out var set, out var sprite, out var xform))
        {
            if (set.RenderOrder != null)
                sprite.RenderOrder = (uint) set.RenderOrder.Value;

            if (set.Offset != null)
            {
                var offset = set.Offset.Value;
                var isPlacementGhost = ghostUid != null && ghostUid == uid;
                var rot = isPlacementGhost ? _placement.Direction.ToAngle() : xform.LocalRotation;

                if (IsWallOffset(offset))
                {
                    var visualDir = (rot + eyeRot).GetCardinalDir();
                    offset = GetWallOffset(visualDir);
                }
                else if (sprite.NoRotation && eyeRot != Angle.Zero)
                {
                    offset = (-eyeRot).RotateVec(offset);
                }

                _sprite.SetOffset((uid, sprite), offset);
            }
        }
    }
}
