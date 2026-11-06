using System.Numerics;
using Content.Shared._Stories.Blood;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Stories.Blood;

public sealed class BloodSplatterEffectSystem : EntitySystem
{
    private const string AnimationKey = "stories-blood-splatter";

    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodSplatterEffectComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<BloodSplatterEffectComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (ent.Owner, spriteComp);
        if (ent.Comp.States.Count > 0)
            _sprite.LayerSetRsiState(sprite, 0, ent.Comp.States[Math.Abs(ent.Comp.Variant) % ent.Comp.States.Count]);

        _sprite.SetRotation(sprite, ent.Comp.Direction);
        _sprite.SetColor(sprite, ent.Comp.Color);

        var offset = ent.Comp.Direction.RotateVec(new Vector2(0, -ent.Comp.Distance));
        var length = (float) ent.Comp.Duration.TotalSeconds;
        var player = EnsureComp<AnimationPlayerComponent>(ent);
        _animation.Play((ent.Owner, player), new Animation
        {
            Length = ent.Comp.Duration,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(offset, length),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(ent.Comp.Color, 0f),
                        new AnimationTrackProperty.KeyFrame(ent.Comp.Color.WithAlpha(0f), length),
                    },
                },
            },
        }, AnimationKey);
    }
}
