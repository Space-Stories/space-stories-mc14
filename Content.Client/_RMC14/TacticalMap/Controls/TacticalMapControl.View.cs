using System;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.TacticalMap.Controls;

public sealed partial class TacticalMapControl
{
    public float GetCurrentZoomFactor()
    {
        return _zoomFactor;
    }

    public Vector2 GetCurrentPanOffset()
    {
        return _panOffset;
    }

    public void SetViewUpdateCallback(Action<float, Vector2> callback)
    {
        _viewUpdateCallback = callback;
    }

    private void NotifyViewChanged()
    {
        _viewUpdateCallback?.Invoke(_zoomFactor, _panOffset);
    }

    public void LoadViewSettings(float zoomFactor, Vector2 panOffset, EntityUid? mapEntity)
    {
        _currentMapEntity = mapEntity;
        _zoomFactor = Math.Clamp(zoomFactor, MinZoom, MaxZoom);
        _panOffset = panOffset;

        ApplyViewSettings();
    }

    public void ApplyViewSettings()
    {
        if (Texture == null)
            return;

        var availableSize = new Vector2(PixelWidth, PixelHeight);
        if (availableSize.X <= 0 || availableSize.Y <= 0)
            return;

        _zoomFactor = Math.Clamp(_zoomFactor, MinZoom, MaxZoom);
        _panOffset = ClampPanOffset(availableSize, _zoomFactor, _panOffset);
    }

    public bool CenterOnPosition(Vector2i indices)
    {
        if (Texture == null)
            return false;

        var availableSize = new Vector2(PixelWidth, PixelHeight);

        if (availableSize.X <= 0 || availableSize.Y <= 0)
            return false;

        var targetPosition = IndicesToPosition(indices);

        var textureSize = Texture.Size;
        var baseScaleX = availableSize.X / textureSize.X;
        var baseScaleY = availableSize.Y / textureSize.Y;
        var baseScale = Math.Min(baseScaleX, baseScaleY);
        var actualScale = baseScale * _zoomFactor;
        var actualSize = textureSize * actualScale;
        var overlayScale = actualScale / MapScale;

        var screenCenter = availableSize / 2;
        _panOffset = screenCenter - targetPosition * overlayScale - (availableSize - actualSize) / 2;
        _panOffset = ClampPanOffset(availableSize, _zoomFactor, _panOffset);

        return true;
    }

    public void ResetZoomAndPan()
    {
        _zoomFactor = FitZoom;
        _panOffset = Vector2.Zero;
        ApplyViewSettings();
        NotifyViewChanged();
    }

    public bool IsAtFitZoom(float? zoomFactor = null)
    {
        return (zoomFactor ?? _zoomFactor) <= FitZoom + FitZoomEpsilon;
    }

    public Vector2 IndicesToPosition(Vector2i indices)
    {
        return GetDrawPosition(indices) * MapScale;
    }

    public Vector2i PositionToIndices(Vector2 controlPosition)
    {
        if (Texture == null)
            return Vector2i.Zero;

        if (!TryGetMapPixelPosition(controlPosition, out var pixelPosition))
            return Vector2i.Zero;

        (Vector2 actualSize, Vector2 actualTopLeft, float overlayScale) = GetDrawParameters();

        var relativeToTexture = (pixelPosition - actualTopLeft) / overlayScale;

        return new Vector2i(
            (int)(relativeToTexture.X / MapScale) + _min.X,
            _delta.Y - (int)(relativeToTexture.Y / MapScale) + _min.Y
        );
    }

    public Vector2 ConvertIndicesToLineCoordinates(Vector2i indices)
    {
        return GetDrawPosition(indices) * MapScale;
    }

    private float GetUIScale()
    {
        return Width > 0 ? PixelWidth / Width : 1.0f;
    }

    private Vector2 LogicalToPixel(Vector2 logicalPosition)
    {
        var uiScale = GetUIScale();
        return logicalPosition * uiScale;
    }

    private Vector2 PixelToLogical(Vector2 pixelPosition)
    {
        var uiScale = GetUIScale();
        return pixelPosition / uiScale;
    }

    private float GetScaledBlipSize(float overlayScale)
    {
        var baseScaledSize = BaseBlipSize * (1.0f + (overlayScale - 1.0f) * 0.6f);
        var zoomReduction = 1.0f / (float)Math.Pow(Math.Max(_zoomFactor, 0.3f), 0.3f);
        return baseScaledSize * zoomReduction * BlipSizeMultiplier;
    }

    private (Vector2 actualSize, Vector2 actualTopLeft, float overlayScale) GetDrawParameters()
    {
        if (Texture == null)
            return (Vector2.Zero, Vector2.Zero, 1.0f);

        var textureSize = Texture.Size;
        var availableSize = new Vector2(PixelWidth, PixelHeight);

        var baseScaleX = availableSize.X / textureSize.X;
        var baseScaleY = availableSize.Y / textureSize.Y;
        var baseScale = Math.Min(baseScaleX, baseScaleY);
        var actualScale = baseScale * _zoomFactor;

        var actualSize = textureSize * actualScale;
        var actualTopLeft = (availableSize - actualSize) / 2 + _panOffset;
        var overlayScale = actualScale / MapScale;

        return (actualSize, actualTopLeft, overlayScale);
    }

    private Vector2 ClampPanOffset(Vector2 availableSize, float zoomFactor, Vector2 panOffset)
    {
        if (Texture == null)
            return Vector2.Zero;

        if (IsAtFitZoom(zoomFactor))
            return Vector2.Zero;

        var textureSize = Texture.Size;
        var baseScaleX = availableSize.X / textureSize.X;
        var baseScaleY = availableSize.Y / textureSize.Y;
        var baseScale = Math.Min(baseScaleX, baseScaleY);
        var actualSize = textureSize * (baseScale * zoomFactor);

        var maxPan = new Vector2(
            Math.Max(0f, (actualSize.X - availableSize.X) * 0.5f),
            Math.Max(0f, (actualSize.Y - availableSize.Y) * 0.5f));

        return new Vector2(
            Math.Clamp(panOffset.X, -maxPan.X, maxPan.X),
            Math.Clamp(panOffset.Y, -maxPan.Y, maxPan.Y));
    }

    private float GetPanResistance(float zoomFactor)
    {
        if (IsAtFitZoom(zoomFactor))
            return 0f;

        if (zoomFactor >= FitZoom + PanResistanceZoomRange)
            return 1f;

        var normalized = (zoomFactor - FitZoom) / PanResistanceZoomRange;
        return MathF.Pow(Math.Clamp(normalized, 0f, 1f), PanResistanceExponent);
    }

    private bool TryGetMapPixelPosition(Vector2 controlPosition, out Vector2 pixelPosition, bool requireInside = false)
    {
        pixelPosition = Vector2.Zero;

        if (Texture == null)
            return false;

        var logicalPixelPosition = LogicalToPixel(controlPosition);
        (Vector2 actualSize, Vector2 actualTopLeft, _) = GetDrawParameters();
        var mapRect = UIBox2.FromDimensions(actualTopLeft, actualSize);

        if (requireInside && !mapRect.Contains(logicalPixelPosition))
            return false;

        pixelPosition = new Vector2(
            Math.Clamp(logicalPixelPosition.X, mapRect.Left, mapRect.Right),
            Math.Clamp(logicalPixelPosition.Y, mapRect.Top, mapRect.Bottom));
        return true;
    }

    private Vector2i GetDrawPosition(Vector2i pos)
    {
        return new Vector2i(pos.X - _min.X, _delta.Y - (pos.Y - _min.Y));
    }

    private Vector2 PositionToLineCoordinatesFloat(Vector2 controlPosition, Vector2 actualTopLeft, float overlayScale)
    {
        var pixelPosition = LogicalToPixel(controlPosition);
        return (pixelPosition - actualTopLeft) / overlayScale;
    }
}
