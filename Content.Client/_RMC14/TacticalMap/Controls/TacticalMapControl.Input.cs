using System;
using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Client._RMC14.TacticalMap.Controls;

public sealed partial class TacticalMapControl
{
    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (HandleQueenEyeClick(args))
                return;

            if (HandleLabelClick(args))
                return;

            if (HandleBlipClick(args))
                return;

            HandleDrawingClick(args);
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            HandlePanningStart(args);
        }
    }

    private bool HandleQueenEyeClick(GUIBoundKeyEventArgs args)
    {
        if (QueenEyeMode && !Drawing && !IsCanvas)
        {
            var clickPosition = PositionToIndices(args.RelativePosition);
            OnQueenEyeMove?.Invoke(clickPosition);
            args.Handle();
            return true;
        }
        return false;
    }

    private bool HandleLabelClick(GUIBoundKeyEventArgs args)
    {
        if (!LabelEditMode || !Drawing || CurrentLabelMode == LabelMode.None)
            return false;

        Vector2i? labelPosition = GetLabelAtPosition(args.RelativePosition);
        if (labelPosition != null)
        {
            if (CurrentLabelMode == LabelMode.Tactical)
            {
                _draggingLabel = labelPosition;
                _labelDragStart = LogicalToPixel(args.RelativePosition).Floored();
                _currentDragPosition = LogicalToPixel(args.RelativePosition);
            }
            args.Handle();
            return true;
        }

        var clickPosition = PositionToIndices(args.RelativePosition);
        ShowLabelInputDialog(clickPosition);
        args.Handle();
        return true;
    }

    private bool HandleBlipClick(GUIBoundKeyEventArgs args)
    {
        var blipIndex = GetBlipIndexAtPosition(args.RelativePosition);
        var allowBlipClick = !QueenEyeMode && !Drawing;

        if (blipIndex != null && _blips != null && allowBlipClick)
        {
            var blip = _blips[blipIndex.Value];
            OnBlipClicked?.Invoke(blip.Indices);
            var entityId = _blipEntityIds != null && blipIndex.Value < _blipEntityIds.Length
                ? _blipEntityIds[blipIndex.Value]
                : (NetEntity?) null;
            OnBlipEntityClicked?.Invoke(blip.Indices, entityId);
            args.Handle();
            return true;
        }
        return false;
    }

    private void HandleDrawingClick(GUIBoundKeyEventArgs args)
    {
        HideTunnelInfo();

        if (Drawing && !LabelEditMode)
        {
            _dragging = true;
            if (!EraserMode)
                BeginStroke();
            var startPixel = LogicalToPixel(args.RelativePosition);
            _dragStart = startPixel.Floored();
            _lastDrag = _dragStart;
            _previewEnd = _dragStart;
            _lastErasePosition = null;

            if (EraserMode)
            {
                _lastErasePosition = startPixel;
                TryEraseAt(args.RelativePosition);
            }

            OnUserInteraction?.Invoke();
            args.Handle();
        }
    }

    private bool HandleLabelRightClick(GUIBoundKeyEventArgs args)
    {
        Vector2i? labelPosition = GetTacticalLabelAtPosition(args.RelativePosition);
        if (labelPosition == null)
            return false;

        string? existingText = TacticalLabels.TryGetValue(labelPosition.Value, out var data)
            ? data.Text
            : null;
        if (string.IsNullOrWhiteSpace(existingText))
            return false;

        ShowLabelInputDialog(labelPosition.Value, existingText);
        args.Handle();
        return true;
    }

    private bool HandleBlipRightClick(GUIBoundKeyEventArgs args)
    {
        TacticalMapBlip? clickedBlip = GetBlipAtPosition(args.RelativePosition);
        if (clickedBlip != null && OnBlipRightClicked != null)
        {
            OnBlipRightClicked.Invoke(clickedBlip.Value.Indices, "");
            args.Handle();
            return true;
        }
        return false;
    }

    private void HandleRightClickAction(GUIBoundKeyEventArgs args)
    {
        if (HandleLabelRightClick(args))
            return;

        if (HandleBlipRightClick(args))
            return;

        HandleContextMenuRequest(args);
    }

    private bool HandleContextMenuRequest(GUIBoundKeyEventArgs args)
    {
        if (OnContextMenuRequested == null || Texture == null)
            return false;

        var clickPosition = PositionToIndices(args.RelativePosition);
        OnContextMenuRequested.Invoke(this, clickPosition, args.PointerLocation.Position);
        args.Handle();
        return true;
    }

    private void HandlePanningStart(GUIBoundKeyEventArgs args)
    {
        HideTunnelInfo();
        _rightClickPanning = true;
        _rightClickMoved = false;
        _rightClickStartPosition = LogicalToPixel(args.RelativePosition);
        _lastPanPosition = _rightClickStartPosition;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (_draggingLabel != null && _labelDragStart != null && _currentDragPosition != null)
            {
                var currentPixelPos = LogicalToPixel(args.RelativePosition);
                var dragDiff = currentPixelPos - _labelDragStart.Value;

                if (dragDiff.Length() > MinDragDistance)
                {
                    var newPosition = PositionToIndices(args.RelativePosition);
                    OnMoveLabel?.Invoke(_draggingLabel.Value, newPosition);
                }
                else
                {
                    string? existingText = GetLabelAt(_draggingLabel.Value);
                    if (existingText != null)
                    {
                        ShowLabelInputDialog(_draggingLabel.Value, existingText);
                    }
                }

                _draggingLabel = null;
                _labelDragStart = null;
                _currentDragPosition = null;
                args.Handle();
            }
            else if (_dragging && Drawing && !LabelEditMode)
            {
                if (!EraserMode && _dragStart != null && (StraightLineMode || SquareMode))
                {
                    var currentPos = LogicalToPixel(args.RelativePosition).Floored();
                    var diff = currentPos - _dragStart.Value;

                    if (diff.Length >= MinDragDistance)
                    {
                        var startIndices = PositionToIndices(PixelToLogical(new Vector2(_dragStart.Value.X, _dragStart.Value.Y)));
                        var endIndices = PositionToIndices(args.RelativePosition);

                        if (StraightLineMode)
                        {
                            endIndices = SnapToStraightLine(startIndices, endIndices);
                            var lineStart = ConvertIndicesToLineCoordinates(startIndices);
                            var lineEnd = ConvertIndicesToLineCoordinates(endIndices);
                            AddLineToCanvas(lineStart, lineEnd, smooth: false);
                        }
                        else if (SquareMode)
                        {
                            AddSquareToCanvas(startIndices, endIndices);
                        }
                    }
                }

                if (!EraserMode)
                    EndStroke();

                _dragging = false;
                _lastDrag = null;
                _dragStart = null;
                _previewEnd = null;
                _lastErasePosition = null;
                args.Handle();
            }
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            if (!_rightClickMoved)
                HandleRightClickAction(args);

            _rightClickPanning = false;
            _lastPanPosition = null;
            _rightClickStartPosition = null;
            _rightClickMoved = false;
            args.Handle();
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _lastMousePosition = args.RelativePosition;

        if (_draggingLabel != null)
        {
            _currentDragPosition = LogicalToPixel(args.RelativePosition);
            args.Handle();
        }
        else if (_dragging && Drawing && !LabelEditMode)
        {
            var currentPixelPos = LogicalToPixel(args.RelativePosition);
            var currentPos = currentPixelPos.Floored();

            if (EraserMode)
            {
                if (_lastErasePosition == null ||
                    (currentPixelPos - _lastErasePosition.Value).Length() >= MinEraserSegmentPixels)
                {
                    TryEraseAt(args.RelativePosition);
                    _lastErasePosition = currentPixelPos;
                }
            }
            else if (StraightLineMode || SquareMode)
            {
                _previewEnd = currentPos;
            }
            else
            {
                if (_lastDrag != null)
                {
                    var diff = currentPos - _lastDrag.Value;
                    if (diff.Length >= MinDragDistance)
                    {
                        if (Texture == null)
                            return;

                        (_, Vector2 actualTopLeft, float overlayScale) = GetDrawParameters();
                        var minSegment = MinFreehandSegmentPixels / Math.Max(overlayScale, 0.001f);

                        var lineStart = PositionToLineCoordinatesFloat(
                            PixelToLogical(new Vector2(_lastDrag.Value.X, _lastDrag.Value.Y)),
                            actualTopLeft,
                            overlayScale);
                        var lineEnd = PositionToLineCoordinatesFloat(
                            args.RelativePosition,
                            actualTopLeft,
                            overlayScale);

                        var delta = lineEnd - lineStart;
                        var distance = delta.Length();
                        if (distance >= minSegment)
                        {
                            var steps = (int)MathF.Ceiling(distance / minSegment);
                            var step = delta / steps;
                            var prev = lineStart;
                            for (int i = 1; i <= steps; i++)
                            {
                                var next = lineStart + step * i;
                                AddLineSegment(prev, next);
                                prev = next;
                            }
                        }
                        _lastDrag = currentPos;
                    }
                }
            }
            args.Handle();
        }
        else if (_rightClickPanning && _lastPanPosition != null)
        {
            var currentPixelPos = LogicalToPixel(args.RelativePosition);

            if (_rightClickStartPosition == null)
            {
                _rightClickStartPosition = currentPixelPos;
                _lastPanPosition = currentPixelPos;
            }

            if (!_rightClickMoved)
            {
                if ((currentPixelPos - _rightClickStartPosition.Value).Length() < MinDragDistance)
                    return;

                if (GetPanResistance(_zoomFactor) <= 0f)
                    return;

                _rightClickMoved = true;
                _lastPanPosition = currentPixelPos;
            }

            var panDelta = currentPixelPos - _lastPanPosition.Value;
            var panResistance = GetPanResistance(_zoomFactor);

            if (panResistance <= 0f)
                return;

            _panOffset += panDelta * panResistance;
            ApplyViewSettings();
            NotifyViewChanged();

            _lastPanPosition = currentPixelPos;
            args.Handle();
        }

        UpdateHoverInfo(args.RelativePosition);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _lastMousePosition = null;
        ClearHoverInfo();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (Texture == null)
            return;

        if (!TryGetMapPixelPosition(args.RelativePosition, out var mousePixelPos, requireInside: true))
            return;

        (Vector2 oldActualSize, Vector2 oldActualTopLeft, float oldOverlayScale) = GetDrawParameters();

        var oldZoom = _zoomFactor;
        var targetZoom = oldZoom;

        if (args.Delta.Y > 0)
            targetZoom *= ZoomSpeed;
        else if (args.Delta.Y < 0)
            targetZoom /= ZoomSpeed;

        targetZoom = Math.Clamp(targetZoom, MinZoom, MaxZoom);

        if (Math.Abs(targetZoom - oldZoom) > 0.001f)
        {
            var relativeToTexture = (mousePixelPos - oldActualTopLeft) / oldOverlayScale;
            var availableSize = new Vector2(PixelWidth, PixelHeight);
            var newPan = Vector2.Zero;

            if (!IsAtFitZoom(targetZoom))
            {
                var textureSize = Texture.Size;
                var baseScale = GetFittedMapScale(availableSize);
                var actualScale = baseScale * targetZoom;
                var actualSize = textureSize * actualScale;
                var overlayScale = actualScale / MapScale;
                var centeredTopLeft = (availableSize - actualSize) / 2;

                newPan = mousePixelPos - relativeToTexture * overlayScale - centeredTopLeft;
            }

            _zoomFactor = targetZoom;
            _panOffset = ClampPanOffset(availableSize, _zoomFactor, newPan);
            NotifyViewChanged();
        }

        args.Handle();
    }

    private TacticalMapBlip? GetBlipAtPosition(Vector2 controlPosition)
    {
        var index = GetBlipIndexAtPosition(controlPosition);
        if (index == null || _blips == null)
            return null;

        return _blips[index.Value];
    }

    private int? GetBlipIndexAtPosition(Vector2 controlPosition)
    {
        if (_blips == null || Texture == null)
            return null;

        var pixelPosition = LogicalToPixel(controlPosition);
        (Vector2 actualSize, Vector2 actualTopLeft, float overlayScale) = GetDrawParameters();
        var clickTolerance = ClickTolerance * overlayScale;

        for (var i = 0; i < _blips.Length; i++)
        {
            var blip = _blips[i];
            var blipPosition = IndicesToPosition(blip.Indices) * overlayScale + actualTopLeft;
            var scaledBlipSize = GetScaledBlipSize(overlayScale);

            UIBox2 blipRect = UIBox2.FromDimensions(
                blipPosition - new Vector2(clickTolerance, clickTolerance),
                new Vector2(scaledBlipSize + clickTolerance * 2, scaledBlipSize + clickTolerance * 2)
            );

            if (blipRect.Contains(pixelPosition))
                return i;
        }

        return null;
    }
}
