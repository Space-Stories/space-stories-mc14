using Content.Shared._RMC14.Projectiles;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared.Projectiles;

namespace Content.Shared._Stories.ZLevels.Throwing;

/// <summary>
/// Keeps z-physics out of the way of a shot physical projectile (arrows, bolts).
/// The gun system launches projectiles with BodyStatus.InAir so tile friction doesn't apply,
/// but z-physics would sync BodyStatus back to OnGround (the projectile is at ground height),
/// making it slide to a stop after a few tiles. Z-physics is resumed once the projectile embeds.
/// </summary>
public sealed partial class ZLevelProjectileSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZPhysicsComponent, ProjectileShotEvent>(OnShot);
        SubscribeLocalEvent<ZPhysicsComponent, EmbedEvent>(OnEmbed);
    }

    private void OnShot(Entity<ZPhysicsComponent> ent, ref ProjectileShotEvent args)
    {
        ent.Comp.Disabled = true;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.Disabled));
    }

    private void OnEmbed(Entity<ZPhysicsComponent> ent, ref EmbedEvent args)
    {
        ent.Comp.Disabled = false;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.Disabled));
    }
}
