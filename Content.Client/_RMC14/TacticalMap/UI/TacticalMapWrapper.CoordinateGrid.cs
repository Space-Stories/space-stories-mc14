using System;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.TacticalMap.UI;

public sealed partial class TacticalMapWrapper
{
    private bool _coordinateGridEdited;
    private bool _applyingCoordinateGrid;
    private bool _canManageCoordinateGrid;

    private void SetupCoordinateGrid()
    {
        CoordinateGridSizeSlider.MinValue = TacticalMapCoordinateGrid.MinCellSize;
        CoordinateGridSizeSlider.MaxValue = TacticalMapCoordinateGrid.MaxCellSize;
        CoordinateGridSizeSlider.Value = TacticalMapCoordinateGrid.DefaultCellSize;
        CoordinateGridCheckBox.OnToggled += _ => OnCoordinateGridChanged();
        CoordinateGridSizeSlider.OnValueChanged += _ => OnCoordinateGridChanged();
        UpdateCoordinateGridControls();
    }

    private TacticalMapCoordinateGrid GetCoordinateGridDraft()
    {
        return new TacticalMapCoordinateGrid(CoordinateGridCheckBox.Pressed,
            (int) MathF.Round(CoordinateGridSizeSlider.Value)).Clamped();
    }

    private void OnCoordinateGridChanged()
    {
        if (_applyingCoordinateGrid || !_canDraw || !_canManageCoordinateGrid)
            return;

        UpdateCoordinateGridControls();
        Canvas.CoordinateGrid = GetCoordinateGridDraft();
        _coordinateGridEdited = true;
        MarkCanvasDirty();
    }

    private void UpdateCoordinateGridControls(int? cellSize = null)
    {
        _applyingCoordinateGrid = true;
        TacticalMapCoordinateGridLayout.GetCellSizeRange(GetCoordinateGridMapSize(), out var minimum, out var maximum);
        CoordinateGridSizeSlider.MinValue = 0;
        CoordinateGridSizeSlider.MaxValue = maximum;
        CoordinateGridSizeSlider.MinValue = minimum;
        CoordinateGridSizeSlider.Value = Math.Clamp(cellSize ?? CoordinateGridSizeSlider.Value, minimum, maximum);
        _applyingCoordinateGrid = false;
        CoordinateGridSizeSlider.Disabled = !CoordinateGridCheckBox.Pressed;
        var layout = TacticalMapCoordinateGridLayout.Create(GetCoordinateGridMapSize(), GetCoordinateGridDraft().CellSize);
        CoordinateGridSizeLabel.Text = Loc.GetString("rmc-tactical-map-coordinate-grid-size",
            ("columns", layout.Columns), ("rows", layout.Rows));
    }

    private Vector2i GetCoordinateGridMapSize()
    {
        return Map.Texture?.Size ?? Canvas.Texture?.Size ?? Vector2i.One;
    }

    public void SetCoordinateGridAccess(bool allowed)
    {
        _canManageCoordinateGrid = allowed;
        CoordinateGridSettings.Visible = allowed && _canDraw;
        if (!allowed)
            _coordinateGridEdited = false;
    }

    public void UpdateCoordinateGrid(TacticalMapCoordinateGrid? visible)
    {
        var published = (visible ?? TacticalMapCoordinateGrid.Default).Clamped(GetCoordinateGridMapSize());
        Map.CoordinateGrid = published;
        if (_coordinateGridEdited && !CanApplyCanvasSnapshot())
            return;

        _applyingCoordinateGrid = true;
        CoordinateGridCheckBox.Pressed = published.Enabled;
        UpdateCoordinateGridControls(published.CellSize);
        _coordinateGridEdited = false;
        Canvas.CoordinateGrid = published;
    }
}
