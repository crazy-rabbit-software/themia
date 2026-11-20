using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14261] Lines cut with their ellipsis inside a scaled block (a meeting's title and its lines, [S-11407]): they take
/// the width the block gives them and never limit its scale. A <see cref="ReadingFit"/> measures its parts unbounded
/// across and scales the block down until the widest text fits; a text the block cuts would stop the scale with its whole
/// length and leave a band under the block (owner, 2026-11-19, WR-4: "they should stretch if they dont reach all the
/// way"). Measured unbounded this panel asks for no more than its <see cref="Least"/> (so a block scaled up never squeezes
/// it to nothing); laid out, it measures its part again at the width it is given, so the part's texts are cut there (their
/// <see cref="TextTrimming"/>), and keeps that width for the passes after.
/// </summary>
public sealed partial class CutLine : Panel
{
    /// <summary>The width the part was last laid out at; NaN before the first layout.</summary>
    private double _width = double.NaN;

    /// <summary>The width the lines ask of their block at most, unscaled: what the block keeps for them however it scales.</summary>
    public double Least
    {
        get => _least;
        set
        {
            if (!value.Equals(_least))
            {
                _least = value;
                InvalidateMeasure();
            }
        }
    }

    private double _least;

    public CutLine(UIElement part)
    {
        ArgumentNullException.ThrowIfNull(part);
        Children.Add(part);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }

        var part = Children[0];
        if (double.IsFinite(availableSize.Width))
        {
            part.Measure(availableSize);
            return new Size(Math.Min(part.DesiredSize.Width, availableSize.Width), part.DesiredSize.Height);
        }

        // Unbounded (the fit measuring its parts): its own width asks the fit for no more than the least, whatever width it
        // was last given (so the scale never feeds back into it); then the part at that width, where its texts are cut.
        part.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var asked = Math.Min(part.DesiredSize.Width, double.IsFinite(Least) ? Math.Max(0, Least) : 0);
        if (double.IsFinite(_width))
        {
            part.Measure(new Size(_width, availableSize.Height));
        }

        return new Size(asked, part.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }

        var part = Children[0];
        if (!finalSize.Width.Equals(_width))
        {
            // The width it stands in now: measured again there, so its texts are cut at it rather than clipped.
            _width = finalSize.Width;
            part.Measure(new Size(finalSize.Width, double.PositiveInfinity));
        }

        part.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }
}
