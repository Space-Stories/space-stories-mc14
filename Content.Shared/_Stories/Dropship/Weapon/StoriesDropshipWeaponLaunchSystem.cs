using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Collections;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Dropship.Weapon;

public sealed class StoriesDropshipWeaponLaunchSystem : EntitySystem
{
    [Dependency] private readonly SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private readonly SharedDropshipSystem _dropship = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stuns = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipWeaponComponent, StoriesDropshipWeaponLaunchedEvent>(OnWeaponLaunched);
    }

    private void OnWeaponLaunched(Entity<DropshipWeaponComponent> ent, ref StoriesDropshipWeaponLaunchedEvent args)
    {
        if (_net.IsClient || ent.Comp.STLaunchCameraShakeStrength <= 0 ||
            !_dropship.TryGetGridDropship(ent, out var dropship))
        {
            return;
        }

        foreach (var session in Filter.BroadcastGrid(dropship.Owner).Recipients)
        {
            if (session.AttachedEntity is not { } passenger)
                continue;

            var strength = ent.Comp.STLaunchCameraShakeStrength;
            _cameraRecoil.KickCamera(passenger, _random.NextVector2Box(-strength, strength));
        }

        if (ent.Comp.STLaunchKnockdownChance <= 0 || ent.Comp.STLaunchKnockdownDuration <= TimeSpan.Zero ||
            !HasComp<PhysicsComponent>(dropship))
        {
            return;
        }

        // Match ShuttleSystem's takeoff/landing selection, including NPCs and excluding contained passengers.
        // Collect first: applying a stun can drop items and change the grid's children.
        var passengers = new ValueList<Entity<StatusEffectsComponent>>();
        var children = Transform(dropship).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp<BuckleComponent>(child, out var buckle) || buckle.Buckled ||
                !TryComp<StatusEffectsComponent>(child, out var status) ||
                !_random.Prob(ent.Comp.STLaunchKnockdownChance))
            {
                continue;
            }

            passengers.Add((child, status));
        }

        foreach (var passenger in passengers)
            _stuns.TryParalyze(passenger, ent.Comp.STLaunchKnockdownDuration, true, passenger.Comp);
    }
}
