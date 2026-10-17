using Content.Shared._RMC14.Tackle;

namespace Content.Shared._Stories.Juggernaut;

/// <summary>Shifts the tackle min/max window up so the wearer resists xeno tackles specifically - other knockdown sources are untouched.</summary>
public sealed class JuggernautTackleResistSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<JuggernautArmorWornComponent, TackleGetThresholdsEvent>(OnGetThresholds);
    }

    private void OnGetThresholds(Entity<JuggernautArmorWornComponent> ent, ref TackleGetThresholdsEvent args)
    {
        // Shift both together - most castes' Max sits below the resist threshold.
        var shift = ent.Comp.TackleResistMin - args.Min;
        if (shift <= 0)
            return;

        args.Min += shift;
        args.Max += shift;
    }
}
