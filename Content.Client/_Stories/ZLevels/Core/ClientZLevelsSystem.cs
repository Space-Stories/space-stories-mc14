using System.Numerics;
using Content.Client._Stories.ZLevels.Core.Overlays;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.StatusEffectNew.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client._Stories.ZLevels.Core;

/// <summary>
/// Only process Eye offset and drawdepth on clientside
/// </summary>
public sealed partial class ClientZLevelsSystem : SharedZLevelsSystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly ClientZLevelsPreAnimSystem _preAnim = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ZLevelBlurOverlay());
        SubscribeLocalEvent<ZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<ZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZPhysicsComponent, AfterAutoHandleStateEvent>(OnZState);
    }

    protected override void OnShutdown(Entity<ZPhysicsComponent> entity, ref ComponentShutdown args)
    {
        base.OnShutdown(entity, ref args);
        _preAnim.UntrackEntity(entity.Owner);
    }

    private void OnZState(Entity<ZPhysicsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.LocalPosition != 0f || ent.Comp.VisualZApplied)
            _preAnim.TrackEntity(ent.Owner);
    }

    protected override void OnZLevelMapMove(Entity<ZPhysicsComponent> ent, ref ZLevelMapMoveEvent args)
    {
        base.OnZLevelMapMove(ent, ref args);

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            _sprite.SetOffset((ent.Owner, sprite), ent.Comp.SpriteOffsetDefault);
            ent.Comp.VisualZApplied = false;
        }

        _preAnim.TrackEntity(ent.Owner);
    }

    private void OnEyeOffset(Entity<ZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        var rotation = -_eye.CurrentEye.Rotation;
        var localPosition = GetVisualLocalPosition(ent, ent.Comp, Transform(ent), ZPhysicsQuery);
        var offset = rotation.RotateVec(new Vector2(0, localPosition * ZLevelOffset));
        args.Offset += offset;
    }

    /// <summary>
    /// Entities riding/parented onto another Z-physics body (e.g. a rider buckled to a flying vehicle)
    /// never step their own LocalPosition, so they must visually follow the parent's height instead.
    /// </summary>
    internal static float GetVisualLocalPosition(EntityUid uid, ZPhysicsComponent? zPhys, TransformComponent? xform, EntityQuery<ZPhysicsComponent> zPhysicsQuery)
    {
        if (zPhys == null)
            return 0f;
        if (zPhys.LocalPosition != 0f)
            return zPhys.LocalPosition;
        if (xform == null)
            return 0f;
        var parent = xform.ParentUid;
        if (parent.IsValid() && zPhysicsQuery.TryComp(parent, out var parentZPhys) && parentZPhys != null)
            return parentZPhys.LocalPosition;
        return 0f;
    }

    private void OnStartup(Entity<ZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        if (sprite.SnapCardinals)
            return;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;

        if (ent.Comp.LocalPosition != 0f || ent.Comp.VisualZApplied)
            _preAnim.TrackEntity(ent.Owner);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ZLevelBlurOverlay>();
    }
}

/// <summary>
/// Pre-animation pass for Z-level visuals.
/// Runs its <see cref="FrameUpdate"/> BEFORE <see cref="AnimationPlayerSystem"/> every render frame,
/// resetting <see cref="SpriteComponent.Offset"/> to the entity's clean base (no Z).
/// </summary>
internal sealed partial class ClientZLevelsPreAnimSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    private EntityQuery<MapGridComponent> _mapGridQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ZPhysicsComponent> _zPhysQuery;
    private readonly HashSet<EntityUid> _activeEntities = new();
    private readonly List<EntityUid> _toRemove = new();
    internal readonly HashSet<EntityUid> ZVisualEntities = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(AnimationPlayerSystem));
        _mapGridQuery = GetEntityQuery<MapGridComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _zPhysQuery = GetEntityQuery<ZPhysicsComponent>();
    }

    public void TrackEntity(EntityUid uid)
    {
        _activeEntities.Add(uid);
    }

    public void UntrackEntity(EntityUid uid)
    {
        _activeEntities.Remove(uid);
        ZVisualEntities.Remove(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        ZVisualEntities.Clear();

        if (_activeEntities.Count == 0)
            return;

        _toRemove.Clear();
        foreach (var uid in _activeEntities)
        {
            if (!_zPhysQuery.TryComp(uid, out var zPhys) ||
                !_spriteQuery.TryComp(uid, out var sprite) ||
                !_xformQuery.TryComp(uid, out var xform))
            {
                _toRemove.Add(uid);
                continue;
            }

            var localPosition = ClientZLevelsSystem.GetVisualLocalPosition(uid, zPhys, xform, _zPhysQuery);
            if (localPosition != 0f || zPhys.VisualZApplied)
            {
                ZVisualEntities.Add(uid);
                if (SharedZLevelsSystem.ZLevelOffset != 0f)
                    _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault);
                _sprite.SetDrawDepth((uid, sprite), localPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault);
                zPhys.VisualZApplied = localPosition != 0f;
            }
            else
            {
                _toRemove.Add(uid);
            }
        }

        foreach (var uid in _toRemove)
        {
            _activeEntities.Remove(uid);
        }

        if (SharedZLevelsSystem.ZLevelOffset != 0f && ZVisualEntities.Count > 0)
        {
            var syncQuery = EntityQueryEnumerator<StatusEffectComponent, SpriteComponent, TransformComponent>();
            while (syncQuery.MoveNext(out var uid, out _, out var sprite, out var xform))
            {
                var parent = xform.ParentUid;
                if (!ZVisualEntities.Contains(parent))
                    continue;
                if (!_zPhysQuery.TryComp(parent, out var parentZPhys))
                    continue;
                var zOffset = new Vector2(0, parentZPhys.LocalPosition * SharedZLevelsSystem.ZLevelOffset);
                _sprite.SetOffset((uid, sprite), zOffset);
            }
        }
    }
}

/// <summary>
/// Post-animation pass for Z-level visuals.
/// Runs its <see cref="FrameUpdate"/> AFTER <see cref="AnimationPlayerSystem"/> so that
/// whatever offset the animation player wrote to <see cref="SpriteComponent.Offset"/> this frame is preserved,
/// and the Z-height contribution is simply added on top.
/// </summary>
internal sealed partial class ClientZLevelsPostAnimSystem : EntitySystem
{
    [Dependency] private readonly ClientZLevelsPreAnimSystem _preAnim = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ZPhysicsComponent> _zPhysQuery;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _zPhysQuery = GetEntityQuery<ZPhysicsComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (SharedZLevelsSystem.ZLevelOffset == 0f || _preAnim.ZVisualEntities.Count == 0)
            return;

        foreach (var uid in _preAnim.ZVisualEntities)
        {
            if (!_zPhysQuery.TryComp(uid, out var zPhys) ||
                !_spriteQuery.TryComp(uid, out var sprite) ||
                !_xformQuery.TryComp(uid, out var xform))
                continue;
            var localPosition = ClientZLevelsSystem.GetVisualLocalPosition(uid, zPhys, xform, _zPhysQuery);
            if (localPosition == 0f)
                continue;
            var rawZ = new Vector2(0, localPosition * SharedZLevelsSystem.ZLevelOffset);
            Vector2 zOffset;
            if (sprite.NoRotation)
                zOffset = rawZ;
            else
                zOffset = new Angle(-_transform.GetWorldRotation(xform)).RotateVec(rawZ);
            _sprite.SetOffset((uid, sprite), sprite.Offset + zOffset);
        }
    }
}
