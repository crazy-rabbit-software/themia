using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>[S-3733] Where a submenu stands beside the levels above it.</summary>
internal partial class MenuPanel
{
    /// <summary>
    /// [S-3733] Put a submenu beside the level before it, its first line level with the line that
    /// opened it, where it covers neither the selected widgets, the bars, nor the levels above it
    /// (<see cref="SubmenuPlacement"/>: the end side, the start side, then slid up or down; the least
    /// covered place only when no place is clear), inside the work area. Measured in the submenus'
    /// own layer, whose end side is the flow's: right to left the sides mirror with it.
    /// </summary>
    private void Place(Level level, int at)
    {
        if (at < 1 || at >= _levels.Count || level.Anchor is not { } anchorView || !_subs.Children.Contains(level.Root))
        {
            return;
        }

        UpdateLayout();

        var size = level.Root.DesiredSize;
        var line = anchorView.Root.TransformToVisual(_subs).TransformPoint(new Point(0, 0));
        var top = line.Y - Fluent.Space1 - Fluent.Hairline;
        var levels = _levels.Take(at).Select(l => BoxInSubs(l.Root)).ToList();

        var avoid = new List<PixelRect>();
        if (_room is { } room)
        {
            if (room.Selection is { } widgets)
            {
                avoid.Add(FromLayer(widgets));
            }

            avoid.AddRange(room.Bars.Select(FromLayer));
        }

        var spot = SubmenuPlacement.Place(levels[^1], top, size.Width, size.Height, RoomArea(), avoid, levels);
        Canvas.SetLeft(level.Root, Math.Round(spot.X));
        Canvas.SetTop(level.Root, Math.Round(spot.Y));
    }

    /// <summary>[S-3733] An element's box in the submenus' layer.</summary>
    private PixelRect BoxInSubs(FrameworkElement element)
    {
        var box = element.TransformToVisual(_subs).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new PixelRect(box.X, box.Y, box.Width, box.Height);
    }

    /// <summary>[S-3733] A rect of the layer the menu stands on, in the submenus' layer.</summary>
    private PixelRect FromLayer(PixelRect rect)
    {
        if (Parent is not UIElement layer)
        {
            return rect;
        }

        var box = layer.TransformToVisual(_subs).TransformBounds(new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        return new PixelRect(box.X, box.Y, box.Width, box.Height);
    }

    /// <summary>[S-3733] The work area in the submenus' layer: the surface's, else the window's.</summary>
    private PixelRect RoomArea()
    {
        if (_room is { } room)
        {
            return FromLayer(room.Area);
        }

        if (XamlRoot is { Content: UIElement content } root)
        {
            var box = content.TransformToVisual(_subs).TransformBounds(new Rect(0, 0, root.Size.Width, root.Size.Height));
            return new PixelRect(box.X, box.Y, box.Width, box.Height);
        }

        return new PixelRect(0, 0, 1280, 800);
    }
}
