using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12808] [S-12814] A one-readout view's block in its room (<see cref="BlockFit.Fill"/>): its parts (a reading and
/// its caption, its controls, a bar) stand together and the block scales up as one to the largest size at which the
/// parts with a width of their own fit the room's width and the parts packed together fit its height; then it is laid
/// out in the whole height left (a <see cref="SpreadStack"/> that justifies: the room between its parts, one part
/// taking it all), its first ink on the room's top and its last on its foot, never a band over or under them. A bar,
/// a slider or a graph spans the width it is given and never limits the scale. The sides' and the foot's insets are
/// this panel's margin (or its view's padding), so they never scale.
/// </summary>
public sealed partial class ReadingFit : Panel
{
    private readonly SpreadStack _plan = new() { Justify = true };
    private readonly ScaleTransform _scale = new();
    private BlockFill _fill = new(1, 0, 0);

    public ReadingFit()
    {
        Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _plan.RenderTransform = _scale;
        Children.Add(_plan);
    }

    /// <summary>
    /// [S-12814] Measure again the fit <paramref name="part"/> stands in, if any: a part whose text grew past the width
    /// it was last measured at keeps its clamped size, so the change never reaches the fit by itself and the scale
    /// that held the old text would cut the new one (a disk's "0 B/s" turning "10 MB/s").
    /// </summary>
    public static void Refit(DependencyObject part)
    {
        for (var node = part is null ? null : VisualTreeHelper.GetParent(part); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ReadingFit fit)
            {
                fit.InvalidateMeasure();
                return;
            }
        }
    }

    /// <summary>The block's parts, top to bottom.</summary>
    public UIElementCollection Parts => _plan.Children;

    /// <summary>The least gap between two parts (a family token).</summary>
    public double Spacing
    {
        get => _plan.Spacing;
        set => _plan.Spacing = value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The parts at their own size: what has a width of its own, and the parts packed.
        var shown = _plan.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        var intrinsic = 0.0;
        var packed = 0.0;
        foreach (var part in shown)
        {
            part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            packed += part.DesiredSize.Height;

            // Measured unbounded, a bar, a slider or a graph asks for next to no width: only text and icons count.
            intrinsic = Math.Max(intrinsic, part.DesiredSize.Width);
        }

        packed += Math.Max(0, shown.Count - 1) * _plan.Spacing;
        // [S-12813] Sized by the reading's ink (a placeholder keeps its reading's size), placed by the ink drawn.
        var held = shown.Count == 0 ? LineInk.None : new LineInk(PartInk.Held(shown[0]).Over, PartInk.Held(shown[^1]).Under);
        var ink = shown.Count == 0 ? LineInk.None : new LineInk(PartInk.Of(shown[0]).Over, PartInk.Of(shown[^1]).Under);
        _fill = BlockFit.Fill(availableSize.Width, availableSize.Height, intrinsic, packed, held, ink);
        _scale.ScaleX = _fill.Scale;
        _scale.ScaleY = _fill.Scale;

        var width = double.IsFinite(availableSize.Width) ? availableSize.Width / _fill.Scale : double.PositiveInfinity;
        _plan.Measure(new Size(width, _fill.Height));
        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : _plan.DesiredSize.Width,
            double.IsFinite(availableSize.Height) ? availableSize.Height : _plan.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _plan.Arrange(new Rect(0, _fill.Top, finalSize.Width / _fill.Scale, _fill.Height));
        return finalSize;
    }
}
