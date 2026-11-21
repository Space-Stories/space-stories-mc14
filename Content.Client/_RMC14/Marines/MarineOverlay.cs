using System.Numerics;
using Content.Shared._RMC14.Marines;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Marines;

public sealed class MarineOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly MarineSystem _marine;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly ShaderInstance _shader;
    private readonly List<MarineHudIcon> _icons = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public MarineOverlay()
    {
        IoCManager.InjectDependencies(this);
        _marine = _entity.System<MarineSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _shader = _prototype.Index<ShaderPrototype>("shaded").Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entity.HasComponent<ShowMarineIconsComponent>(_players.LocalEntity))
            return;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var xformQuery = _entity.GetEntityQuery<TransformComponent>();
        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);
        handle.UseShader(_shader);

        var marineQuery = _entity.AllEntityQueryEnumerator<MarineComponent, StatusIconComponent, SpriteComponent, TransformComponent>();
        while (marineQuery.MoveNext(out var uid, out _, out var status, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var bounds = status.Bounds ?? _sprite.GetLocalBounds((uid, sprite));
            var worldPos = _transform.GetWorldPosition(xform, xformQuery);
            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            _marine.GetHudIcons(uid, _icons);
            if (_icons.Count == 0)
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);
            foreach (var icon in _icons)
                handle.DrawTexture(icon.Texture, icon.Position, icon.Modulate);
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }
}
