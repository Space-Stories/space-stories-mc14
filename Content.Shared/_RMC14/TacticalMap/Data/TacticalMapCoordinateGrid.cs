using System;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.TacticalMap;

[DataRecord]
[Serializable, NetSerializable]
public readonly record struct TacticalMapCoordinateGrid(bool Enabled, int CellSize)
{
    public const int MinCellSize = 1;
    public const int MaxCellSize = 1024;
    public const int DefaultCellSize = 32;

    public static readonly TacticalMapCoordinateGrid Default = new(false, DefaultCellSize);

    public TacticalMapCoordinateGrid Clamped()
    {
        return this with { CellSize = Math.Clamp(CellSize, MinCellSize, MaxCellSize) };
    }

    public TacticalMapCoordinateGrid Clamped(Vector2i mapSize)
    {
        return this with { CellSize = TacticalMapCoordinateGridLayout.Create(mapSize, CellSize).CellSize };
    }
}
