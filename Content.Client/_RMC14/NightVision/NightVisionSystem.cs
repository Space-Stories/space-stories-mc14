using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Burrow;
using Content.Shared.Examine;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._RMC14.NightVision;

public sealed class NightVisionSystem : SharedNightVisionSystem
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly ILightManager _light = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private EntityQuery<XenoComponent> _xenoQuery;
    private EntityQuery<NightVisionComponent> _nvQuery;

    // Stories-MesonPerf-Start
    private const float MesonSpriteUpdateInterval = 0.25f;
    private float _mesonSpriteUpdateAccumulator;
    // Stories-MesonPerf-End

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnNightVisionAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnNightVisionDetached);

        _xenoQuery = _entity.GetEntityQuery<XenoComponent>();
        _nvQuery = _entity.GetEntityQuery<NightVisionComponent>();
    }

    private void OnNightVisionAttached(Entity<NightVisionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        NightVisionChanged(ent);
    }

    private void OnNightVisionDetached(Entity<NightVisionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        Off();
    }

    protected override void NightVisionChanged(Entity<NightVisionComponent> ent)
    {
        if (ent != _player.LocalEntity)
            return;

        switch (ent.Comp.State)
        {
            case NightVisionState.Off:
                Off();
                break;
            case NightVisionState.Half:
                Half(ent);
                break;
            case NightVisionState.Full:
                Full(ent);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void NightVisionRemoved(Entity<NightVisionComponent> ent)
    {
        if (ent != _player.LocalEntity)
            return;

        Off();
    }

    private void SetMesons(bool on)
    {
        if (_player.LocalEntity == null)
            return;

        _eye.SetDrawFov(_player.LocalEntity.Value, !on);
    }

    private void Off()
    {
        _overlay.RemoveOverlay<NightVisionOverlay>();
        _overlay.RemoveOverlay<NightVisionFilterOverlay>();
        _overlay.RemoveOverlay<HalfNightVisionBrightnessOverlay>();
        _light.DrawLighting = true;
        SetMesons(false);
        SetMesonSprites(false);
    }

    private void Half(Entity<NightVisionComponent> ent)
    {
        if (ent.Comp.Overlay)
            _overlay.AddOverlay(new NightVisionOverlay());

        if (ent.Comp.Green)
            _overlay.AddOverlay(new NightVisionFilterOverlay());

        _overlay.AddOverlay(new HalfNightVisionBrightnessOverlay());

        _light.DrawLighting = true;
        SetMesons(ent.Comp.Mesons);
    }

    private void Full(Entity<NightVisionComponent> ent)
    {
        if (ent.Comp.Overlay)
            _overlay.AddOverlay(new NightVisionOverlay());

        if (ent.Comp.Green)
            _overlay.AddOverlay(new NightVisionFilterOverlay());

        _light.DrawLighting = false;
        SetMesons(ent.Comp.Mesons);
    }

    private void SetMesonSprites(bool mesons)
    {
        // Stories-MesonPerf-Start
        var local = _player.LocalEntity;
        if (local == null && mesons)
            return;

        var isXeno = local != null && _xenoQuery.HasComp(local.Value);
        // Stories-MesonPerf-End

        var query = EntityQueryEnumerator<RMCMesonsNonviewableComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var viewable, out var sprite))
        {
            if (isXeno && viewable.XenoVisible)
            {
                sprite.Visible = true;
                continue;
            }

            if (TryComp<XenoBurrowComponent>(uid, out var burrow) && burrow.Active)
                continue;

            sprite.Visible = !mesons || _examine.InRangeUnOccluded(local!.Value, uid); // Stories-MesonPerf
        }
    }

    public override void Update(float frameTime)
    {
        if (_player.LocalEntity == null)
            return;

        if (!_nvQuery.TryComp(_player.LocalEntity.Value, out var nightVision))
            return;

        if (nightVision.State == NightVisionState.Off)
            return;

        // Stories-MesonPerf-Start
        _mesonSpriteUpdateAccumulator += frameTime;
        if (_mesonSpriteUpdateAccumulator < MesonSpriteUpdateInterval)
            return;

        _mesonSpriteUpdateAccumulator = 0f;
        // Stories-MesonPerf-End
        SetMesonSprites(nightVision.Mesons);
    }
}
