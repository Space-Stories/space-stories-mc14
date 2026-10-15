using Content.Shared._RMC14.Synth;
using Content.Shared.DragDrop;

namespace Content.Shared._Stories.Synth;

public sealed class SharedSTSyntheticMaintenanceStationSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<STSyntheticMaintenanceStationComponent, CanDropTargetEvent>(OnCanDropTarget);
    }

    private void OnCanDropTarget(Entity<STSyntheticMaintenanceStationComponent> ent, ref CanDropTargetEvent args)
    {
        if (ent.Comp.Occupied || !HasComp<SynthComponent>(args.Dragged))
            return;

        args.Handled = true;
        args.CanDrop = true;
    }
}
