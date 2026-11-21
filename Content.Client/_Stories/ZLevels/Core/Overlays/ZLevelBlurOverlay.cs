using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.ZLevels.Core.Overlays;

public sealed partial class ZLevelBlurOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    private readonly ShaderInstance? _blurShader;

    // Cached ambient color to avoid per-frame TryGetComponent
    private EntityUid? _lastAmbientMapUid;
    private Robust.Shared.Maths.Vector3 _cachedAmbientColor = new(1, 1, 1);

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ProtoId<ShaderPrototype> _zBlurShader = "ZBlur";

    public ZLevelBlurOverlay()
    {
        IoCManager.InjectDependencies(this);
        _blurShader = _proto.Index(_zBlurShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_cfg.GetCVar(CCVars.ZLevelsRenderingBlurShader))
            return false;

        if (args.Viewport.Eye is not ScalingViewport.ZEye zeye)
            return false;

        if (args.MapId == MapId.Nullspace)
            return false;

        // If looking UP (HighestDepth > 0): blur all levels below HighestDepth
        if (zeye.HighestDepth > 0)
        {
            return zeye.Depth < zeye.HighestDepth;
        }

        // If looking DOWN (LowestDepth < 0): blur lower levels below Depth 0
        if (zeye.LowestDepth < 0)
        {
            return zeye.Depth < 0;
        }

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        // Cache ambient color per map — avoids TryGetComponent every frame
        if (args.MapUid != _lastAmbientMapUid)
        {
            _lastAmbientMapUid = args.MapUid;
            _cachedAmbientColor = new Robust.Shared.Maths.Vector3(1, 1, 1);

            if (_entity.TryGetComponent<MapLightComponent>(args.MapUid, out var mapLight))
            {
                _cachedAmbientColor = new Robust.Shared.Maths.Vector3(
                    mapLight.AmbientLightColor.R,
                    mapLight.AmbientLightColor.G,
                    mapLight.AmbientLightColor.B);
            }
        }

        _blurShader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _blurShader?.SetParameter("BLUR_COLOR", _cachedAmbientColor);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_blurShader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
