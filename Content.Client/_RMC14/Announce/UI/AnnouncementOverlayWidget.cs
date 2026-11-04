using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Announce;

public sealed class AnnouncementOverlayWidget : UIWidget
{
    private const float StackSeparation = 16f;

    private readonly LayoutContainer _layout;
    private readonly List<AnnouncementWidget> _announcements = new();
    private readonly HashSet<AnnouncementWidget> _standaloneAnnouncements = new();
    private readonly List<AnnouncementWidget> _stackAnnouncements = new();
    private readonly List<AnnouncementWidget> _positionedAnnouncements = new();
    private readonly List<Vector2> _stackSizes = new();
    private readonly List<UIBox2> _occupiedBounds = new();

    public IReadOnlyList<AnnouncementWidget> Announcements => _announcements;

    public AnnouncementOverlayWidget()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

        _layout = new AnnouncementLayoutContainer(this)
        {
            MouseFilter = MouseFilterMode.Ignore,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        LayoutContainer.SetAnchorPreset(_layout, LayoutContainer.LayoutPreset.Wide);
        AddChild(_layout);
    }

    public void AddAnnouncement(AnnouncementWidget widget)
    {
        AddAnnouncement(widget, true);
    }

    public void AddStandaloneAnnouncement(AnnouncementWidget widget)
    {
        AddAnnouncement(widget, false);
    }

    private void AddAnnouncement(AnnouncementWidget widget, bool stacked)
    {
        widget.PositionManaged = true;
        _announcements.Add(widget);
        if (!stacked)
            _standaloneAnnouncements.Add(widget);

        _layout.AddChild(widget);
        Visible = true;
        Reflow();
    }

    public void RemoveAnnouncement(AnnouncementWidget widget)
    {
        _announcements.Remove(widget);
        _standaloneAnnouncements.Remove(widget);
        widget.Parent?.RemoveChild(widget);
        Visible = _announcements.Count > 0;
        Reflow();
    }

    public void ClearAnnouncements()
    {
        foreach (var widget in _announcements.ToArray())
        {
            widget.Parent?.RemoveChild(widget);
        }

        _announcements.Clear();
        _standaloneAnnouncements.Clear();
        _stackAnnouncements.Clear();
        _positionedAnnouncements.Clear();
        _stackSizes.Clear();
        _occupiedBounds.Clear();
        Visible = false;
    }

    public void Reflow()
    {
        _positionedAnnouncements.Clear();
        _occupiedBounds.Clear();
        // Reserve centered announcements first, regardless of their arrival order.
        ReflowStacks(centered: true, standalone: false);
        ReflowStacks(centered: true, standalone: true);
        ReflowStacks(centered: false, standalone: false);
        ReflowStacks(centered: false, standalone: true);
        _stackAnnouncements.Clear();
        _stackSizes.Clear();
        _positionedAnnouncements.Clear();
        _occupiedBounds.Clear();
    }

    private void ReflowStacks(bool centered, bool standalone)
    {
        for (var i = 0; i < _announcements.Count; i++)
        {
            var announcement = _announcements[i];
            if (announcement.ActiveAnnouncement == null ||
                (announcement.ResolveStackAlignment() == HAlignment.Center) != centered ||
                _standaloneAnnouncements.Contains(announcement) != standalone ||
                _positionedAnnouncements.Contains(announcement))
            {
                continue;
            }

            _stackAnnouncements.Clear();
            _stackSizes.Clear();
            for (var j = i; j < _announcements.Count; j++)
            {
                var candidate = _announcements[j];
                if (j == i || !standalone && !_standaloneAnnouncements.Contains(candidate) &&
                    CanShareStack(announcement, candidate))
                {
                    _stackAnnouncements.Add(candidate);
                    _stackSizes.Add(candidate.ResolveLayout().Size);
                    _positionedAnnouncements.Add(candidate);
                }
            }

            var visibleCount = _stackAnnouncements.Count;
            while (visibleCount > 0 && !TryPlaceStack(visibleCount))
                visibleCount--;

            for (var j = 0; j < _stackAnnouncements.Count; j++)
                _stackAnnouncements[j].SetLayoutVisible(j < visibleCount);
        }
    }

    private static bool CanShareStack(AnnouncementWidget first, AnnouncementWidget second)
    {
        if (first.ActiveAnnouncement is not { } firstAnnouncement ||
            second.ActiveAnnouncement is not { } secondAnnouncement)
        {
            return false;
        }

        if (firstAnnouncement.Data.ScreenPositionOverride is { } customPosition)
            return secondAnnouncement.Data.ScreenPositionOverride == customPosition;

        return secondAnnouncement.Data.ScreenPositionOverride == null &&
               firstAnnouncement.ResolvedStyle.LayoutConfig.Position ==
               secondAnnouncement.ResolvedStyle.LayoutConfig.Position;
    }

    private bool TryPlaceStack(int count)
    {
        var stackWidth = 0f;
        var stackHeight = 0f;
        for (var i = 0; i < count; i++)
        {
            stackWidth = MathF.Max(stackWidth, _stackSizes[i].X);
            stackHeight += _stackSizes[i].Y;
        }

        stackHeight += StackSeparation * (count - 1);
        var stackSize = new Vector2(stackWidth, stackHeight);
        var first = _stackAnnouncements[0];
        var viewport = first.ResolveViewportBounds();
        var position = first.ResolveLayout(stackSize).Position;
        position.Y = count == 1
            ? Math.Clamp(position.Y, viewport.Top, MathF.Max(viewport.Top, viewport.Bottom - stackHeight))
            : MathF.Max(position.Y, viewport.Top);
        var bounds = UIBox2.FromDimensions(position, stackSize);

        // Moving below an obstacle can encounter another stack, so check again after every shift.
        for (var i = 0; i < _occupiedBounds.Count; i++)
        {
            var obstacle = _occupiedBounds[i];
            if (!bounds.Intersects(obstacle))
                continue;

            position.Y = obstacle.Bottom + StackSeparation;
            bounds = UIBox2.FromDimensions(position, stackSize);
            i = -1;
        }

        if (bounds.Bottom > viewport.Bottom)
            return false;

        var alignment = first.ResolveStackAlignment();
        var currentY = position.Y;
        for (var i = 0; i < count; i++)
        {
            var size = _stackSizes[i];
            var x = alignment switch
            {
                HAlignment.Right => position.X + stackWidth - size.X,
                HAlignment.Center => position.X + (stackWidth - size.X) * 0.5f,
                _ => position.X
            };

            _stackAnnouncements[i].ApplyManagedLayout(new Vector2(x, currentY), size);
            currentY += size.Y + StackSeparation;
        }

        _occupiedBounds.Add(bounds);
        return true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Reflow();
    }

    private sealed class AnnouncementLayoutContainer(AnnouncementOverlayWidget overlay) : LayoutContainer
    {
        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            // Text can grow during the child widgets' frame updates, after the overlay has reflowed.
            overlay.Reflow();
            return base.ArrangeOverride(finalSize);
        }
    }
}
