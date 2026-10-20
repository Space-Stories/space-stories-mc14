using System.Numerics;
using Content.Client._RMC14.NightVision;
using Content.Shared._RMC14.Mobs;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.Banish;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Xenonids.Banish;

public sealed class STXenoBanishedOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ContainerSystem _container;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<EntityActiveInvisibleComponent> _invisQuery;

    private readonly ShaderInstance _shader;

    private readonly SpriteSpecifier.Rsi _icon = new(new ResPath("/Textures/_RMC14/Interface/xeno_hud.rsi"), "xeno_banished");

    public override OverlaySpace Space => _overlay.HasOverlay<NightVisionOverlay>()
        ? OverlaySpace.WorldSpace
        : OverlaySpace.WorldSpaceBelowFOV;

    public STXenoBanishedOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();

        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _invisQuery = _entity.GetEntityQuery<EntityActiveInvisibleComponent>();

        _shader = _prototype.Index<ShaderPrototype>("unshaded").Instance();

        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entity.HasComponent<XenoComponent>(_players.LocalEntity) &&
            !_entity.HasComponent<CMGhostXenoHudComponent>(_players.LocalEntity))
        {
            return;
        }

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        var banished = _entity.EntityQueryEnumerator<STXenoBanishedComponent, SpriteComponent, TransformComponent>();
        while (banished.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            if (_invisQuery.HasComp(uid))
                continue;

            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(_icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
        }

        handle.UseShader(null);
    }
}
