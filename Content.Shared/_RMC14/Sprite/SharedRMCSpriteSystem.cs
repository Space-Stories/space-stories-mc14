using System.Numerics;
using Content.Shared._RMC14.Hands;

namespace Content.Shared._RMC14.Sprite;

public abstract class SharedRMCSpriteSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpriteSetRenderOrderComponent, ComponentStartup>(OnSetRenderOrderStartup);
        SubscribeLocalEvent<SpriteSetRenderOrderComponent, MapInitEvent>(OnSetRenderOrderMapInit);
        SubscribeLocalEvent<SpriteSetRenderOrderComponent, ItemPickedUpEvent>(OnSetRenderOrderPickedUp);
    }

    private void OnSetRenderOrderStartup(Entity<SpriteSetRenderOrderComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnSetRenderOrderMapInit(Entity<SpriteSetRenderOrderComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<SpriteSetRenderOrderComponent> ent)
    {
        if (ent.Comp.RenderOrder != null)
            _appearance.SetData(ent, SpriteSetRenderOrderComponent.Appearance.Key, ent.Comp.RenderOrder);

        if (ent.Comp.Offset != null)
            _appearance.SetData(ent, SpriteSetRenderOrderComponent.Appearance.Offset, ent.Comp.Offset);
    }

    private void OnSetRenderOrderPickedUp(Entity<SpriteSetRenderOrderComponent> ent, ref ItemPickedUpEvent args)
    {
        if (ent.Comp.Offset == null || ent.Comp.Offset == Vector2.Zero)
            return;

        ent.Comp.Offset = Vector2.Zero;
        Dirty(ent);
        _appearance.SetData(ent, SpriteSetRenderOrderComponent.Appearance.Offset, Vector2.Zero);
    }

    public void SetOffset(EntityUid ent, Vector2 offset)
    {
        var sprite = EnsureComp<SpriteSetRenderOrderComponent>(ent);
        sprite.Offset = offset;
        Dirty(ent, sprite);
        _appearance.SetData(ent, SpriteSetRenderOrderComponent.Appearance.Offset, offset); // Stories-LightOffset
    }

    public void SetRenderOrder(EntityUid ent, int order)
    {
        var sprite = EnsureComp<SpriteSetRenderOrderComponent>(ent);
        sprite.RenderOrder = order;
        Dirty(ent, sprite);
        _appearance.SetData(ent, SpriteSetRenderOrderComponent.Appearance.Key, order); // Stories-LightOffset
    }

    public void SetColor(Entity<SpriteColorComponent?> ent, Color color)
    {
        ent.Comp = EnsureComp<SpriteColorComponent>(ent);
        ent.Comp.Color = color;
        Dirty(ent);
    }

    public DrawDepth.DrawDepth GetDrawDepth(EntityUid ent, DrawDepth.DrawDepth current = DrawDepth.DrawDepth.Mobs)
    {
        var ev = new GetDrawDepthEvent(current);
        RaiseLocalEvent(ent, ref ev);
        return ev.DrawDepth;
    }

    public virtual DrawDepth.DrawDepth UpdateDrawDepth(EntityUid sprite)
    {
        var depth = GetDrawDepth(sprite);
        _appearance.SetData(sprite, RMCSpriteDrawDepth.Key, depth);
        return depth;
    }
}
