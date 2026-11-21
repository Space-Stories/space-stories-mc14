using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Content.Shared._RMC14.TacticalMap;
using Robust.Client.Graphics;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.TacticalMap.Controls;

public sealed partial class TacticalMapControl
{
    private readonly Dictionary<int, string> _coordinateColumns = new();
    private readonly Dictionary<int, string> _coordinateRows = new();
    private string _coordinateAlphabet = string.Empty;

    public TacticalMapCoordinateGrid CoordinateGrid { get; set; } = TacticalMapCoordinateGrid.Default;

    public static string FormatCoordinateGridColumn(int column, string alphabet)
    {
        if (column < 0 || alphabet.Length < 2)
            return string.Empty;

        var label = new StringBuilder();
        for (var index = column; index >= 0; index = index / alphabet.Length - 1)
            label.Insert(0, alphabet[index % alphabet.Length]);

        return label.ToString();
    }

    private float GetFittedMapScale(Vector2 availableSize)
    {
        var size = Texture?.Size ?? Vector2i.One;
        if (CoordinateGrid.Enabled)
            availableSize = Vector2.Max(Vector2.One, availableSize - new Vector2(GetCoordinateGridHeaderMargin() * 2f));
        return Math.Min(availableSize.X / size.X, availableSize.Y / size.Y);
    }

    private float GetCoordinateGridHeaderMargin()
    {
        return _font.GetLineHeight(UIScale) + 4f * UIScale;
    }

    private bool TryGetCoordinateGridView(Vector2 mapTopLeft, Vector2 mapSize,
        out TacticalMapCoordinateGridLayout layout, out UIBox2 rectangle, out Vector2 cellPixels)
    {
        layout = default;
        rectangle = default;
        cellPixels = Vector2.Zero;
        if (!CoordinateGrid.Enabled || Texture == null)
            return false;

        layout = TacticalMapCoordinateGridLayout.Create(Texture.Size, CoordinateGrid.CellSize);
        cellPixels = layout.CellDimensions * (mapSize / new Vector2(Texture.Size.X, Texture.Size.Y));
        rectangle = UIBox2.FromDimensions(mapTopLeft, mapSize);
        return Math.Min(cellPixels.X, cellPixels.Y) >= GetCoordinateGridHeaderMargin();
    }

    private void DrawCoordinateGridBackground(DrawingHandleScreen handle, Vector2 mapTopLeft, Vector2 mapSize)
    {
        if (TryGetCoordinateGridView(mapTopLeft, mapSize, out _, out var rectangle, out _))
            handle.DrawRect(rectangle, Color.Black.WithAlpha(0.4f));
    }

    private void DrawCoordinateGrid(DrawingHandleScreen handle, Vector2 mapTopLeft, Vector2 mapSize)
    {
        if (!TryGetCoordinateGridView(mapTopLeft, mapSize, out var layout, out var rectangle, out var cellPixels))
            return;

        _coordinateGridShader.SetParameter("grid_cells", new Vector2(layout.Columns, layout.Rows));
        _coordinateGridShader.SetParameter("cell_pixels", cellPixels);
        handle.UseShader(_coordinateGridShader);
        handle.DrawRect(rectangle, Color.White);
        handle.UseShader(null);
    }

    private void DrawCoordinateGridHeaders(DrawingHandleScreen handle, Vector2 mapTopLeft, Vector2 mapSize)
    {
        if (!TryGetCoordinateGridView(mapTopLeft, mapSize, out var layout, out var rectangle, out var cellPixels))
            return;

        var left = Math.Max(0f, rectangle.Left);
        var top = Math.Max(0f, rectangle.Top);
        var right = Math.Min(PixelWidth, rectangle.Right);
        var bottom = Math.Min(PixelHeight, rectangle.Bottom);
        if (right <= left || bottom <= top)
            return;

        var alphabet = Loc.GetString("rmc-tactical-map-coordinate-grid-alphabet");
        if (_coordinateAlphabet != alphabet)
        {
            _coordinateAlphabet = alphabet;
            _coordinateColumns.Clear();
            _coordinateRows.Clear();
        }

        var padding = 2f * UIScale;
        var headerMargin = GetCoordinateGridHeaderMargin();
        var columnTop = Math.Max(0f, rectangle.Top - headerMargin) + padding;
        var rowLeft = Math.Max(0f, rectangle.Left - headerMargin) + padding;
        var firstColumn = Math.Max(0, (int) MathF.Floor((left - rectangle.Left) / cellPixels.X));
        var lastColumn = Math.Min(layout.Columns - 1, (int) MathF.Floor((right - rectangle.Left) / cellPixels.X));
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            if (!_coordinateColumns.TryGetValue(column, out var label))
            {
                label = FormatCoordinateGridColumn(column, alphabet);
                _coordinateColumns[column] = label;
            }

            var textSize = handle.GetDimensions(_font, label, UIScale);
            var centerX = rectangle.Left + (column + 0.5f) * cellPixels.X;
            if (centerX - textSize.X / 2f < left + padding || centerX + textSize.X / 2f > right - padding)
                continue;

            DrawCoordinateGridHeader(handle, label,
                new Vector2(centerX - textSize.X / 2f, columnTop), textSize, padding);
        }

        var firstRow = Math.Max(0, (int) MathF.Floor((top - rectangle.Top) / cellPixels.Y));
        var lastRow = Math.Min(layout.Rows - 1, (int) MathF.Floor((bottom - rectangle.Top) / cellPixels.Y));
        for (var row = firstRow; row <= lastRow; row++)
        {
            if (!_coordinateRows.TryGetValue(row, out var label))
            {
                label = (row + 1).ToString(CultureInfo.InvariantCulture);
                _coordinateRows[row] = label;
            }

            var textSize = handle.GetDimensions(_font, label, UIScale);
            var centerY = rectangle.Top + (row + 0.5f) * cellPixels.Y;
            if (centerY - textSize.Y / 2f < top + padding || centerY + textSize.Y / 2f > bottom - padding)
                continue;

            DrawCoordinateGridHeader(handle, label,
                new Vector2(rowLeft, centerY - textSize.Y / 2f), textSize, padding);
        }
    }

    private void DrawCoordinateGridHeader(DrawingHandleScreen handle, string label,
        Vector2 position, Vector2 size, float padding)
    {
        handle.DrawRect(UIBox2.FromDimensions(position - new Vector2(padding), size + new Vector2(padding * 2f)),
            Color.Black.WithAlpha(0.65f));
        handle.DrawString(_font, position, label, UIScale, Color.White.WithAlpha(0.85f));
    }
}
