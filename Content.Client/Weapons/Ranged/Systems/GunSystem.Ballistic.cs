using Content.Client.Stack;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private readonly StackSystem _stack = default!;

    protected override void InitializeBallistic()
    {
        base.InitializeBallistic();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, UpdateAmmoCounterEvent>(OnBallisticAmmoCount);
        // Stories-RMCAmmoEject-Start
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AfterAutoHandleStateEvent>(OnBallisticHandleState);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AppearanceChangeEvent>(OnBallisticAppearance);
        // Stories-RMCAmmoEject-End
    }

    // Stories-RMCAmmoEject-Start
    private void OnBallisticHandleState(EntityUid uid, BallisticAmmoProviderComponent component, ref AfterAutoHandleStateEvent args)
    {
        try
        {
            UpdateAmmoCount(uid, prediction: false);
        }
        catch (Exception e)
        {
            Log.Error($"[GunSystem] Error handling ballistic state for {ToPrettyString(uid)}: {e}");
        }
    }

    private void OnBallisticAppearance(EntityUid uid, BallisticAmmoProviderComponent component, ref AppearanceChangeEvent args)
    {
        UpdateAmmoCount(uid, prediction: false);
    }
    // Stories-RMCAmmoEject-End

    private void OnBallisticAmmoCount(EntityUid uid, BallisticAmmoProviderComponent component, UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
        {
            var count = GetBallisticShots(component);
            // Stories-RMCAmmoEject-Start
            if (TryComp<AppearanceComponent>(uid, out var appearance) &&
                Appearance.TryGetData<int>(uid, AmmoVisuals.AmmoCount, out var appCount, appearance))
            {
                count = appCount;
            }
            // Stories-RMCAmmoEject-End
            control.Update(count + args.ArtificialIncrease, component.Capacity);
        }
    }

    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        EntityUid? ent = null;

        // TODO: Combine with TakeAmmo
        if (component.Entities.Count > 0)
        {
            var existing = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);

            Containers.Remove(existing, component.Container);
            EnsureShootable(existing);
            ent = existing; // Stories-BallisticCyclePredictionFix
        }
        else if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            ent = Spawn(component.Proto, coordinates);
            _stack.SetCount(ent.Value, 1);
            EnsureShootable(ent.Value);
        }

        if (ent != null && IsClientSide(ent.Value))
            Del(ent.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }
}
