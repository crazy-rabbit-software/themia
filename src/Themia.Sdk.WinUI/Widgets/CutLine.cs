using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14261] Lines cut with their ellipsis inside a scaled block (a meeting's title and its lines, [S-11407]): they take
/// the width the block gives them and never limit its scale. A <see cref="ReadingFit"/> measures its parts unbounded
/// across and scales the block down until the widest text fits; a text the block cuts would stop the scale with its whole
/// length and leave a band under the block (owner, 2026-11-19, WR-4: "they should stretch if they dont reach all the
/// way"). Measured unbounded this panel asks for no more than its <see cref="Least"/> (so a block scaled up never squeezes
/// it to nothing); laid out, it measures its part again at the width it is given, so the part's texts are cut there (their
/// <see cref="TextTrimming"/>), and keeps that width for the passes after. [S-14791] The line it <see cref="Holds"/> (a title) still
/// keeps the block from growing past where it stands whole: its <see cref="Whole"/> width is the fit's most.
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

    /// <summary>
    /// [S-14791] The line in the part whose whole width the block never grows past (the meeting's title): the fit scales the
    /// block no larger than where it stands uncut, and cuts it only at its floor (<see cref="BlockLayout.Uncut"/>). Null: none.
    /// </summary>
    public FrameworkElement? Holds { get; init; }

    /// <summary>
    /// [S-14791] The width this panel would ask, unscaled, for <see cref="Holds"/> to stand uncut (never under what it asks):
    /// as last measured unbounded.
    /// </summary>
    public double Whole { get; private set; }

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

        // [S-14791] Measured unbounded, the held line is its whole width: read before it is cut again below.
        Whole = Holds is { Visibility: Visibility.Visible } held ? Math.Max(asked, held.DesiredSize.Width) : asked;
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
