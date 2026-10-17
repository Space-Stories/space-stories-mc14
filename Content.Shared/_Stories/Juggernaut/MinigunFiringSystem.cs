using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Tracks whether a <see cref="MinigunFiringVisualsComponent"/> gun is actively shooting and mirrors that onto its <see cref="MinigunFiringVisuals.Firing"/> appearance data, so the client can swap its wielded in-hand sprite to the animated firing state.</summary>
public sealed class MinigunFiringSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MinigunFiringVisualsComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<MinigunFiringVisualsComponent> ent, ref GunShotEvent args)
    {
        var wasFiring = ent.Comp.LastShotAt != null;
        ent.Comp.LastShotAt = _timing.CurTime;
        Dirty(ent);

        if (!wasFiring)
            _appearance.SetData(ent, MinigunFiringVisuals.Firing, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MinigunFiringVisualsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.LastShotAt is not { } last || now - last <= comp.GraceAfterShot)
                continue;

            comp.LastShotAt = null;
            Dirty(uid, comp);
            _appearance.SetData(uid, MinigunFiringVisuals.Firing, false);
        }
    }
}
