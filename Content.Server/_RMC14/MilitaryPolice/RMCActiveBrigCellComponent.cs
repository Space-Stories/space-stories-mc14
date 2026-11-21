namespace Content.Server._RMC14.MilitaryPolice;

/// <summary>Marks a cell with a timed sentence whose deadline must be processed.</summary>
[RegisterComponent, Access(typeof(RMCBrigCellSystem))]
public sealed partial class RMCActiveBrigCellComponent : Component;
