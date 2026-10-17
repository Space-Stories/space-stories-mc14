using Content.Shared._Stories.Juggernaut;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Stories.Juggernaut;

/// <summary>Client-side half of <see cref="MinigunFiringVisualsComponent"/> - while <see cref="MinigunFiringVisuals.Firing"/> is set, swaps whichever wielded in-hand layer the holder already has to the animated "-firing" RSI state, then back once fire stops.</summary>
public sealed class MinigunFiringVisualsSystem : VisualizerSystem<MinigunFiringVisualsComponent>
{
    private static readonly string[] Locations = ["left", "right"];

    [Dependency] private readonly SharedContainerSystem _container = default!;

    protected override void OnAppearanceChange(EntityUid uid, MinigunFiringVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp(uid, out ItemComponent? item))
            return;

        if (!_container.TryGetContainingContainer((uid, null, null), out var container))
            return;

        if (!TryComp(container.Owner, out SpriteComponent? holderSprite))
            return;

        var firing = AppearanceSystem.TryGetData<bool>(uid, MinigunFiringVisuals.Firing, out var isFiring, args.Component) && isFiring;

        foreach (var location in Locations)
        {
            var baseState = item.HeldPrefix == null ? $"inhand-{location}" : $"{item.HeldPrefix}-inhand-{location}";

            if (!SpriteSystem.LayerMapTryGet((container.Owner, holderSprite), baseState, out var index, false))
                continue;

            SpriteSystem.LayerSetRsiState((container.Owner, holderSprite), index, firing ? $"{baseState}-firing" : baseState);
        }
    }
}
