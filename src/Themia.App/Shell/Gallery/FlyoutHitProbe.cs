#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2346] Debug only: whether an edit bar flyout takes every press over it. A grid of points across the flyout's
/// box, each hit-tested as the window would (<see cref="VisualTreeHelper.FindElementsInHostCoordinates(Point, UIElement)"/>,
/// topmost first): a point where the flyout is hit at all must find the flyout's own element on top, never a card,
/// the desktop or anything else drawn over it; and no point inside the panel may miss it. The renders print the count.
/// </summary>
internal static class FlyoutHitProbe
{
    /// <summary>The points tried, those the flyout did not take, and the first such point with what took it.</summary>
    public static (int Points, int Missed, string? First) Run(UIElement root, FrameworkElement flyout, double top, double inset, double step = 5)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(flyout);

        var toHost = flyout.TransformToVisual(null);
        var (points, missed) = (0, 0);
        string? first = null;
        for (var y = top + inset; y <= flyout.ActualHeight - inset; y += step)
        {
            for (var x = inset; x <= flyout.ActualWidth - inset; x += step)
            {
                points++;
                var at = toHost.TransformPoint(new Point(x, y));
                // A plain foreach over the WinRT list: LINQ over its projection needs a helper type AOT has not got.
                UIElement? top1 = null;
                foreach (var hit in VisualTreeHelper.FindElementsInHostCoordinates(at, root))
                {
                    top1 = hit;
                    break;
                }

                if (top1 is not null && Within(top1, flyout))
                {
                    continue;
                }

                missed++;
                first ??= $"({x:0},{y:0}) {(top1 is null ? "nothing" : top1.GetType().Name)}";
            }
        }

        return (points, missed, first);
    }

    private static bool Within(DependencyObject element, DependencyObject flyout)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, flyout))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
