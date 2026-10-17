using Content.Shared._RMC14.Repairable;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Tools.Systems;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Lets a Squire weld the M134C-JLCW while the Juggernaut is holding it, by relaying the interaction to the held gun.</summary>
public sealed class JuggernautAssistedRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<JuggernautWhitelistComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<JuggernautWhitelistComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<SquireWhitelistComponent>(args.User))
            return;

        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!HasComp<JuggernautGunComponent>(held))
                continue;

            if (!TryComp(held, out RMCRepairableComponent? repairable) ||
                !TryComp(held, out DamageableComponent? damageable) ||
                damageable.TotalDamage <= FixedPoint2.Zero)
            {
                continue;
            }

            if (!_tool.HasQuality(args.Used, repairable.Quality))
                continue;

            var relayed = new InteractUsingEvent(args.User, args.Used, held, args.ClickLocation);
            RaiseLocalEvent(held, relayed, true);

            if (relayed.Handled)
            {
                args.Handled = true;
                return;
            }
        }
    }
}
