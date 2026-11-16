using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12400] [S-12410] A stack whose items fill it edge to edge (<see cref="ItemStretch.Lay"/>): each child
/// measured at its natural size, then stretched toward its own most (its fixed <c>Height</c>/<c>Width</c>:
/// none; its <c>MaxHeight</c>/<c>MaxWidth</c>: a family row's half again; else any room), and what the
/// children cannot take shared evenly round them, so the items together reach both ends of the stack and
/// none is left huddled at its start. A collapsed child takes no slot and no gap. Short of room it is a
/// plain stack.
/// </summary>
public partial class SpreadStack : Panel
{
    private Orientation _orientation = Orientation.Vertical;
    private double _spacing;

    /// <summary>Down (the default) or across.</summary>
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (value != _orientation)
            {
                _orientation = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The fixed gap between two items (a family token); the spread adds to it.</summary>
    public double Spacing
    {
        get => _spacing;
        set
        {
            if (value != _spacing)
            {
                _spacing = value;
                InvalidateMeasure();
            }
        }
    }

    private bool Down => _orientation == Orientation.Vertical;

    protected override Size MeasureOverride(Size availableSize)
    {
        var along = 0.0;
        var across = 0.0;
        var shown = 0;
        var room = Down ? new Size(availableSize.Width, double.PositiveInfinity) : new Size(double.PositiveInfinity, availableSize.Height);
        foreach (var child in Children)
        {
            child.Measure(room);
            if (child.Visibility != Visibility.Visible)
            {
                continue;
            }

            shown++;
            along += Down ? child.DesiredSize.Height : child.DesiredSize.Width;
            across = Math.Max(across, Down ? child.DesiredSize.Width : child.DesiredSize.Height);
        }

        along += Math.Max(0, shown - 1) * _spacing;
        return Down ? new Size(across, along) : new Size(along, across);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var shown = Children.Where(c => c.Visibility == Visibility.Visible).ToList();
        var items = shown.Select(Item).ToList();
        var slots = ItemStretch.Lay(Down ? finalSize.Height : finalSize.Width, items, _spacing, StretchRest.Around, XamlRoot?.RasterizationScale ?? 1);
        for (var i = 0; i < shown.Count; i++)
        {
            shown[i].Arrange(Down
                ? new Rect(0, slots[i].Start, finalSize.Width, slots[i].Length)
                : new Rect(slots[i].Start, 0, slots[i].Length, finalSize.Height));
        }

        foreach (var child in Children.Where(c => c.Visibility != Visibility.Visible))
        {
            child.Arrange(default);
        }

        return finalSize;
    }

    /// <summary>A child's natural length along the stack and the most it stretches to.</summary>
    private StretchItem Item(UIElement child)
    {
        var natural = Down ? child.DesiredSize.Height : child.DesiredSize.Width;
        if (child is not FrameworkElement element)
        {
            return StretchItem.Free(natural);
        }

        var own = Down ? element.Height : element.Width;
        var most = Down ? element.MaxHeight : element.MaxWidth;
        var margin = Down ? element.Margin.Top + element.Margin.Bottom : element.Margin.Left + element.Margin.Right;
        return double.IsFinite(own) ? StretchItem.Fixed(natural)
            : double.IsFinite(most) ? new StretchItem(natural, Math.Max(natural, most + margin))
            : StretchItem.Free(natural);
    }
}
