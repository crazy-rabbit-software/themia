using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12804] [S-12808] A one-readout face scaled up as one to fill its box (<see cref="BlockFit"/>), as a display clock
/// is: its ink (<see cref="PartInk"/>: the digits' capitals and baseline, not their line's leading) fills the box's
/// height, or its width stops it and it stands centred on its ink in the height left. [S-12833] A smaller box scales it
/// down as one, never under its smallest drawn text's floor (<see cref="ScaleFloors"/>) and never over <see cref="Most"/>.
/// Across, it stands where <see cref="Align"/> says; the panel itself always takes its whole box.
/// </summary>
public sealed partial class InkFit : Panel
{
    private readonly ScaleTransform _scale = new();
    private HorizontalAlignment _align = HorizontalAlignment.Center;

    /// <summary>Where the face stands across the box: at the start, the middle or the end.</summary>
    public HorizontalAlignment Align
    {
        get => _align;
        set
        {
            if (value != _align)
            {
                _align = value;
                InvalidateArrange();
            }
        }
    }

    /// <summary>
    /// [S-12833] The most the face is drawn at (<see cref="FaceFit.Most"/>): once a part of it gave way, a smaller room never
    /// draws the rest larger than it was when the part went. Unbounded while every part is drawn.
    /// </summary>
    public double Most
    {
        get => _most;
        set
        {
            if (value != _most)
            {
                _most = value;
                InvalidateArrange();
            }
        }
    }

    private double _most = double.PositiveInfinity;

    /// <summary>The face.</summary>
    public FrameworkElement? Child
    {
        get => Children.Count > 0 ? Children[0] as FrameworkElement : null;
        set
        {
            Children.Clear();
            if (value is not null)
            {
                value.RenderTransform = _scale;
                Children.Add(value);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }

        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(Math.Min(child.DesiredSize.Width, availableSize.Width), Math.Min(child.DesiredSize.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }

        var size = child.DesiredSize;
        var lay = BlockFit.Lay(finalSize.Width, finalSize.Height, size.Width, size.Height, PartInk.Of(child), ScaleFloors.Of([child]), _most);
        _scale.ScaleX = lay.Scale;
        _scale.ScaleY = lay.Scale;
        var drawn = size.Width * lay.Scale;
        var left = _align switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Right => finalSize.Width - drawn,
            _ => (finalSize.Width - drawn) / 2,
        };

        child.Arrange(new Rect(left, lay.Top, size.Width, size.Height));
        return finalSize;
    }
}
