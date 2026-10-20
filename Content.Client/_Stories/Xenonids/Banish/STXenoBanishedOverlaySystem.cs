using Robust.Client.Graphics;

namespace Content.Client._Stories.Xenonids.Banish;

public sealed class STXenoBanishedOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        if (!_overlay.HasOverlay<STXenoBanishedOverlay>())
            _overlay.AddOverlay(new STXenoBanishedOverlay());
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<STXenoBanishedOverlay>();
    }
}
