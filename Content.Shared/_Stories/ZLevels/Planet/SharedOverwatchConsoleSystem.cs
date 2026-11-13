using Content.Shared._Stories.ZLevels.Planet;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Overwatch;

public abstract partial class SharedOverwatchConsoleSystem
{
    private void InitializeZLevelInput()
    {
        Subs.BuiEvents<OverwatchConsoleComponent>(OverwatchConsoleUI.Key, subs =>
        {
            subs.Event<OverwatchSupplyDropZLevelBuiMsg>(OnSupplyDropZLevel);
            subs.Event<OverwatchOrbitalZLevelBuiMsg>(OnOrbitalZLevel);
        });
    }

    private void OnSupplyDropZLevel(Entity<OverwatchConsoleComponent> ent, ref OverwatchSupplyDropZLevelBuiMsg args)
    {
        _supplyDrop.SetZLevel(ent.Owner, args.ZLevel);
    }

    private void OnOrbitalZLevel(Entity<OverwatchConsoleComponent> ent, ref OverwatchOrbitalZLevelBuiMsg args)
    {
        ent.Comp.OrbitalZ = args.ZLevel;
        Dirty(ent);
    }
}
