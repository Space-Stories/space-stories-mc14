using Content.Shared._Stories.ZLevels.Planet;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.SupplyDrop;

public abstract partial class SharedSupplyDropSystem
{
    private void InitializeZLevelInput()
    {
        Subs.BuiEvents<SupplyDropComputerComponent>(SupplyDropComputerUi.Key,
            subs => subs.Event<SupplyDropComputerZLevelBuiMsg>(OnSupplyDropZLevel));
    }

    private void OnSupplyDropZLevel(Entity<SupplyDropComputerComponent> ent, ref SupplyDropComputerZLevelBuiMsg args)
    {
        SetZLevel((ent.Owner, ent.Comp), args.ZLevel);
    }

    public void SetZLevel(Entity<SupplyDropComputerComponent?> computer, int zLevel)
    {
        if (!Resolve(computer, ref computer.Comp, false))
            return;

        computer.Comp.TargetZ = zLevel;
        Dirty(computer);
    }
}
