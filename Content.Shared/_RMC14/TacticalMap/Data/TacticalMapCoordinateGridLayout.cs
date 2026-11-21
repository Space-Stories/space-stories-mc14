using System;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.TacticalMap;

public readonly record struct TacticalMapCoordinateGridLayout(int CellSize, int Columns, int Rows, Vector2i Size)
{
    private const int MinCells = 4;
    private const int MaxCells = 12;

    public Vector2 CellDimensions => new((float) Size.X / Columns, (float) Size.Y / Rows);

    public static void GetCellSizeRange(Vector2i mapSize, out int minimum, out int maximum)
    {
        var shortest = Math.Max(1, Math.Min(mapSize.X, mapSize.Y));
        var longest = Math.Max(shortest, Math.Max(mapSize.X, mapSize.Y));
        minimum = Math.Clamp((longest - 1) / MaxCells + 1,
            TacticalMapCoordinateGrid.MinCellSize, TacticalMapCoordinateGrid.MaxCellSize);
        maximum = Math.Clamp((shortest - 1) / MinCells + 1, minimum, TacticalMapCoordinateGrid.MaxCellSize);
    }

    public static TacticalMapCoordinateGridLayout Create(Vector2i mapSize, int requestedSize)
    {
        GetCellSizeRange(mapSize, out var minimum, out var maximum);
        var cellSize = Math.Clamp(requestedSize, minimum, maximum);
        var size = new Vector2i(Math.Max(1, mapSize.X), Math.Max(1, mapSize.Y));
        return new TacticalMapCoordinateGridLayout(cellSize,
            Math.Max(1, (int) MathF.Round((float) size.X / cellSize)),
            Math.Max(1, (int) MathF.Round((float) size.Y / cellSize)), size);
    }
}
