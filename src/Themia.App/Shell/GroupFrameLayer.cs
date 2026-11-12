using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-2453] How a group's frame is drawn: neutral in edit mode, accent while selected, faint accent while entered.</summary>
internal enum GroupFrameLook
{
    Neutral = 0,
    Selected = 1,
    Entered = 2,
}

/// <summary>[S-2453] One group's frame: the box round its members' painted boxes, in DIPs on the surface.</summary>
internal readonly record struct GroupFrame(PixelRect Box, GroupFrameLook Look);

/// <summary>
/// [S-2453] The frames of a surface's groups in edit mode: the edit markers' brackets and edge bars
/// round the union of a group's members, with no grips (a group is moved, never resized; its members
/// draw no markers of their own until the group is entered). Two plates as the cards' markers are, so
/// the frames read as the same marks; never hit-testable.
/// </summary>
internal sealed class GroupFrameLayer
{
    /// <summary>The frames' contrast strokes, drawn first.</summary>
    public Canvas Halos { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>The frames' bars, over the halos.</summary>
    public Canvas Inks { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>
    /// [S-2453] The frames of <paramref name="view"/>'s groups as the cards stand now (their live rects,
    /// so a frame follows a drag): one per group with a visible member. Outside edit mode there are none.
    /// </summary>
    public static List<GroupFrame> Frames(ViewState view, IReadOnlyList<WidgetCard> cards, ScreenMetrics metrics, double gapPx, bool editMode, WidgetSelection selection)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(selection);

        var frames = new List<GroupFrame>();
        if (!editMode || !metrics.IsUsable)
        {
            return frames;
        }

        var here = string.Equals(selection.ViewId, view.Id, StringComparison.Ordinal);
        foreach (var group in view.Groups)
        {
            var boxes = cards
                .Where(c => c.Visibility == Visibility.Visible && string.Equals(c.Model.Group, group.Id, StringComparison.Ordinal))
                .Select(c => GridGeometry.Inset(GridGeometry.ToPixels(c.LiveRect, metrics), gapPx))
                .ToList();
            if (boxes.Count == 0)
            {
                continue;
            }

            var left = boxes.Min(b => b.X);
            var top = boxes.Min(b => b.Y);
            var box = new PixelRect(left, top, boxes.Max(b => b.X + b.Width) - left, boxes.Max(b => b.Y + b.Height) - top);

            var members = WidgetGroups.Members(view, group.Id);
            var look = here && string.Equals(selection.Entered, group.Id, StringComparison.Ordinal) ? GroupFrameLook.Entered
                : here && selection.Entered is null && members.TrueForAll(m => selection.Contains(m.Id)) ? GroupFrameLook.Selected
                : GroupFrameLook.Neutral;
            frames.Add(new GroupFrame(box, look));
        }

        return frames;
    }

    /// <summary>Draw exactly these frames, replacing the ones drawn before.</summary>
    public void Draw(IReadOnlyList<GroupFrame> frames, double scale)
    {
        ArgumentNullException.ThrowIfNull(frames);

        Halos.Children.Clear();
        Inks.Children.Clear();

        var accent = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        foreach (var frame in frames)
        {
            var ink = frame.Look == GroupFrameLook.Neutral ? WidgetCard.NeutralInk() : accent;
            var opacity = frame.Look == GroupFrameLook.Entered ? 0.45 : 1.0;
            foreach (var marker in EditMarkers.For(frame.Box.Width, frame.Box.Height, scale))
            {
                foreach (var bar in marker.Bars)
                {
                    var onSurface = new PixelRect(frame.Box.X + marker.Hit.X + bar.X, frame.Box.Y + marker.Hit.Y + bar.Y, bar.Width, bar.Height);
                    Halos.Children.Add(Bar(EditMarkers.Haloed(onSurface, scale), WidgetCard.MarkerHalo, opacity));
                    Inks.Children.Add(Bar(onSurface, ink, opacity));
                }
            }
        }
    }

    private static Rectangle Bar(PixelRect box, Microsoft.UI.Xaml.Media.Brush fill, double opacity)
    {
        var bar = new Rectangle { Fill = fill, Width = box.Width, Height = box.Height, Opacity = opacity, IsHitTestVisible = false };
        Canvas.SetLeft(bar, box.X);
        Canvas.SetTop(bar, box.Y);
        return bar;
    }
}
