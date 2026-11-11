using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Maths;
using Color = Robust.Shared.Maths.Color;
using Content.Client._RMC14.TacticalMap.UI;

namespace Content.Client._RMC14.TacticalMap.Controls;

public sealed partial class TacticalMapControl
{
    private LabelTextDialog? _labelDialog;

    private Vector2i? GetLabelAtPosition(Vector2 controlPosition)
    {
        if (Texture == null)
            return null;

        if (CurrentLabelMode == LabelMode.None)
            return null;

        var pixelPosition = LogicalToPixel(controlPosition);
        (Vector2 actualSize, Vector2 actualTopLeft, float overlayScale) = GetDrawParameters();
        var clickTolerance = LabelClickTolerance * overlayScale;

        Vector2i? CheckLabels(Dictionary<Vector2i, TacticalMapLabelData> labels, float yOffsetMultiplier)
        {
            foreach ((Vector2i indices, TacticalMapLabelData data) in labels)
            {
                var label = data.Text;
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var position = IndicesToPosition(indices) * overlayScale + actualTopLeft;
                position = position with { Y = position.Y - (LabelYOffset + LabelStackOffset * yOffsetMultiplier) * overlayScale };

                var fontSize = GetLabelFontSize(overlayScale);
                var textWidth = label.Length * fontSize * 0.6f;
                var textHeight = fontSize;
                var textSize = new Vector2(textWidth, textHeight);
                UIBox2 labelRect = UIBox2.FromDimensions(
                    position - textSize / 2 - new Vector2(clickTolerance),
                    textSize + new Vector2(clickTolerance * 2)
                );

                if (labelRect.Contains(pixelPosition))
                    return indices;
            }

            return null;
        }

        Vector2i? CheckAreaLabels(Dictionary<Vector2i, string> labels, float yOffsetMultiplier)
        {
            foreach ((Vector2i indices, string label) in labels)
            {
                var position = IndicesToPosition(indices) * overlayScale + actualTopLeft;
                position = position with { Y = position.Y - (LabelYOffset + LabelStackOffset * yOffsetMultiplier) * overlayScale };

                var fontSize = GetLabelFontSize(overlayScale);
                var textWidth = label.Length * fontSize * 0.6f;
                var textHeight = fontSize;
                var textSize = new Vector2(textWidth, textHeight);
                UIBox2 labelRect = UIBox2.FromDimensions(
                    position - textSize / 2 - new Vector2(clickTolerance),
                    textSize + new Vector2(clickTolerance * 2)
                );

                if (labelRect.Contains(pixelPosition))
                    return indices;
            }

            return null;
        }

        if (CurrentLabelMode == LabelMode.All)
        {
            return CheckLabels(TacticalLabels, 1f) ?? CheckAreaLabels(_areaLabels, 0f);
        }

        if (CurrentLabelMode == LabelMode.Area)
        {
            return CheckAreaLabels(_areaLabels, 0f);
        }

        return CheckLabels(TacticalLabels, 0f);
    }

    private Vector2i? GetTacticalLabelAtPosition(Vector2 controlPosition)
    {
        if (CurrentLabelMode == LabelMode.Area || CurrentLabelMode == LabelMode.None)
            return null;

        if (Texture == null)
            return null;

        var pixelPosition = LogicalToPixel(controlPosition);
        (Vector2 actualSize, Vector2 actualTopLeft, float overlayScale) = GetDrawParameters();
        var clickTolerance = LabelClickTolerance * overlayScale;

        foreach ((Vector2i indices, TacticalMapLabelData data) in TacticalLabels)
        {
            var label = data.Text;
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var position = IndicesToPosition(indices) * overlayScale + actualTopLeft;
            var stackOffset = CurrentLabelMode == LabelMode.All && _areaLabels.ContainsKey(indices)
                ? LabelStackOffset
                : 0f;
            position = position with { Y = position.Y - (LabelYOffset + stackOffset) * overlayScale };

            var fontSize = GetLabelFontSize(overlayScale);
            var textWidth = label.Length * fontSize * 0.6f;
            var textHeight = fontSize;
            var textSize = new Vector2(textWidth, textHeight);
            UIBox2 labelRect = UIBox2.FromDimensions(
                position - textSize / 2 - new Vector2(clickTolerance),
                textSize + new Vector2(clickTolerance * 2)
            );

            if (labelRect.Contains(pixelPosition))
                return indices;
        }

        return null;
    }

    private string? GetLabelAt(Vector2i position)
    {
        if (CurrentLabelMode == LabelMode.None)
            return null;

        if (CurrentLabelMode == LabelMode.Tactical)
        {
            return TacticalLabels.TryGetValue(position, out var tacticalData)
                ? tacticalData.Text
                : null;
        }

        if (CurrentLabelMode == LabelMode.Area)
            return _areaLabels.GetValueOrDefault(position);

        if (TacticalLabels.TryGetValue(position, out var data))
            return data.Text;

        return _areaLabels.GetValueOrDefault(position);
    }

    private void ShowLabelInputDialog(Vector2i position, string existingText = "")
    {
        var map = _currentAreaGridEntity;
        var mapName = _currentMapName;
        _labelDialog?.Dispose();
        _labelDialog = LabelTextDialog.Show(
            position,
            existingText,
            onConfirmed: text =>
            {
                if (_currentAreaGridEntity != map || _currentMapName != mapName)
                    return;

                if (string.IsNullOrWhiteSpace(text))
                {
                    if (!string.IsNullOrEmpty(existingText))
                        OnDeleteLabel?.Invoke(position);
                }
                else
                {
                    if (string.IsNullOrEmpty(existingText))
                        OnCreateLabel?.Invoke(position, text);
                    else
                        OnEditLabel?.Invoke(position, text);
                }
            },
            onDeleted: () =>
            {
                if (_currentAreaGridEntity == map && _currentMapName == mapName)
                    OnDeleteLabel?.Invoke(position);
            }
        );
    }

    public void RequestTacticalLabelEditor(Vector2i position)
    {
        var existingText = TacticalLabels.TryGetValue(position, out var data) ? data.Text : string.Empty;
        ShowLabelInputDialog(position, existingText);
    }

    private static Color GetTacticalLabelColor(TacticalMapLabelData data)
    {
        return data.Color.A <= 0f ? DefaultTacticalLabelColor : data.Color;
    }
}
