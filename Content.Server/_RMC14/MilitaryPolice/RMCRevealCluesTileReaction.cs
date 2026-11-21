using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.Server._RMC14.MilitaryPolice;

[DataDefinition]
public sealed partial class RMCRevealCluesTileReaction : ITileReaction
{
    public FixedPoint2 TileReact(TileRef tile, ReagentPrototype reagent, FixedPoint2 reactVolume,
        IEntityManager entityManager, List<ReagentData>? data = null)
    {
        if (reactVolume <= 0)
            return FixedPoint2.Zero;
        entityManager.System<RMCPoliceClueSystem>().RevealTile(tile);
        return FixedPoint2.Zero;
    }
}
