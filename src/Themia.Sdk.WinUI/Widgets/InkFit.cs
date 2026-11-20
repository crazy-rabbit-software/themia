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
/// down as one, never under its smallest drawn text's floor (<see cref="ScaleFloors"/>).
/// Across, its ink stands where <see cref="Align"/> says (<see cref="PartInk.Across"/>); the panel itself always takes its
/// whole box.
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

                // [S-14301] Drawn scaled, the face is laid out unrounded: a pixel of rounding in its own units is the scale's
                // pixels drawn.
                value.UseLayoutRounding = false;
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

        // [S-14301] Its width is its ink's (a text's side bearings, drawn at the face's scale, are no part of what fills the
        // width), and its ink, not its box, stands where the alignment puts it.
        var size = child.DesiredSize;
        var (start, end) = PartInk.Across(child);
        var ink = Math.Max(1, size.Width - start - end);
        var lay = BlockFit.Lay(finalSize.Width, finalSize.Height, ink, size.Height, PartInk.Of(child), ScaleFloors.Of([child]));
        _scale.ScaleX = lay.Scale;
        _scale.ScaleY = lay.Scale;
        var drawn = ink * lay.Scale;
        var left = _align switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Right => finalSize.Width - drawn,
            _ => (finalSize.Width - drawn) / 2,
        };

        child.Arrange(new Rect(left - (start * lay.Scale), lay.Top, size.Width, size.Height));
        return finalSize;
    }
}
