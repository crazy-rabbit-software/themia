using Themia.Core.Geometry;

namespace Themia.Core.Widgets;

/// <summary>[S-6261] Where a style's picture (a battery, signal bars) stands against its figure.</summary>
public enum PictureArrangement
{
    /// <summary>The picture on top, the figure on the line under it.</summary>
    Under = 0,

    /// <summary>A wide strip: the picture on the left, the figure beside it.</summary>
    Beside = 1,

    /// <summary>No room for a picture worth drawing: the figure alone.</summary>
    FigureOnly = 2,
}

/// <summary>[S-6261] A picture-and-figure face for one box: the picture's size and the figure's step.</summary>
public readonly record struct PicturePlan(PictureArrangement Arrangement, double PictureWidth, double PictureHeight, TypeStep Figure);

/// <summary>
/// [S-6261] The ladder of a style that draws its reading as a picture (the battery's shape, the ping's
/// signal bars) with the figure beside or under it. The picture is the core content and takes what the
/// figure leaves; the figure grows by the tier's step ([S-3860]), so it is the same size as every
/// meter's number at that tier.
/// </summary>
public static class PictureFace
{
    /// <summary>Between the picture and the figure.</summary>
    public const double Gap = 8;

    /// <summary>Under this height a picture is a smudge: the figure is shown alone.</summary>
    public const double MinPicture = 20;

    /// <summary>Beside, the figure's column takes at most this share of the width.</summary>
    public const double FigureShare = 0.45;

    /// <summary>Under, the figure's line takes at most this share of the height.</summary>
    public const double FigureLineShare = 0.4;

    /// <summary>
    /// The plan for a content box (the card already deflated). <paramref name="aspect"/> is the
    /// picture's width per height; <paramref name="ceiling"/> the tier's step ([S-3860]).
    /// </summary>
    public static PicturePlan Plan(WidgetArea box, double aspect, HeroBound figure, bool wide, TypeStep ceiling)
    {
        var width = Math.Max(0, box.Width);
        var height = Math.Max(0, box.Height);
        aspect = aspect > 0 && double.IsFinite(aspect) ? aspect : 1;

        if (wide)
        {
            var step = HeroLayout.Fit(width * FigureShare, height, figure.Value, figure.Unit, FluentType.Subtitle, ceiling);
            var text = HeroLayout.ValueWidth(step, figure.Value, figure.Unit);
            var (w, h) = Inside(width - text - Gap, height, aspect);
            if (h >= MinPicture)
            {
                return new PicturePlan(PictureArrangement.Beside, w, h, step);
            }
        }
        else
        {
            var step = HeroLayout.Fit(width, height * FigureLineShare, figure.Value, figure.Unit, FluentType.Subtitle, ceiling);
            var (w, h) = Inside(width, height - step.LineHeight - Gap, aspect);
            if (h >= MinPicture)
            {
                return new PicturePlan(PictureArrangement.Under, w, h, step);
            }
        }

        return new PicturePlan(PictureArrangement.FigureOnly, 0, 0,
            HeroLayout.Fit(width, height, figure.Value, figure.Unit, FluentType.Subtitle, FluentType.Display));
    }

    /// <summary>The largest box of <paramref name="aspect"/> inside a room, whole DIPs.</summary>
    public static (double Width, double Height) Inside(double width, double height, double aspect)
    {
        if (!(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return (0, 0);
        }

        var h = Math.Floor(Math.Min(height, width / aspect));
        return (Math.Floor(h * aspect), h);
    }
}

/// <summary>[S-6262] A battery drawn as a battery: its body, the terminal cap and the fill inside.</summary>
/// <param name="Body">The body's outline, in DIPs from the picture's top-left.</param>
/// <param name="Cap">The terminal on the body's right.</param>
/// <param name="Stroke">The outline's thickness.</param>
/// <param name="Corner">The body's corner radius.</param>
/// <param name="Inner">Where the fill may go: the body inside its outline and a gap.</param>
/// <param name="Bolt">The charging bolt's glyph size, centred on <paramref name="Inner"/>.</param>
public readonly record struct BatteryShapePlan(PixelRect Body, PixelRect Cap, double Stroke, double Corner, PixelRect Inner, double Bolt);

/// <summary>
/// [S-6262] The battery style's picture, as Windows' own battery glyph is drawn: a rounded body
/// twice as wide as it is tall, a terminal cap at its right, an outline and, inside it, a fill
/// from the left to the charge.
/// </summary>
public static class BatteryShape
{
    /// <summary>The body's width per height.</summary>
    public const double BodyAspect = 2;

    /// <summary>The cap's width as a share of the body's height.</summary>
    public const double CapWidthShare = 0.1;

    /// <summary>The cap's height as a share of the body's height.</summary>
    public const double CapHeightShare = 0.4;

    /// <summary>The whole picture's width per height: the body and its cap.</summary>
    public const double Aspect = BodyAspect + CapWidthShare;

    /// <summary>The shape for a picture box (already of <see cref="Aspect"/> or not: it is fitted and centred).</summary>
    public static BatteryShapePlan Fit(double width, double height)
    {
        var (w, h) = PictureFace.Inside(width, height, Aspect);
        if (h <= 0)
        {
            return default;
        }

        var x = Math.Floor((Math.Max(0, width) - w) / 2);
        var y = Math.Floor((Math.Max(0, height) - h) / 2);
        var cap = Math.Max(2, Math.Round(h * CapWidthShare));
        var bodyWidth = w - cap;
        var stroke = Math.Clamp(Math.Round(h * 0.06), 2, 6);
        var corner = Math.Round(h * 0.16);
        var capHeight = Math.Round(h * CapHeightShare);

        // The fill stands clear of the outline by a gap of about the stroke, as the glyph's does.
        var pad = stroke + Math.Max(1, Math.Round(stroke * 0.8));
        var inner = new PixelRect(x + pad, y + pad, Math.Max(0, bodyWidth - (2 * pad)), Math.Max(0, h - (2 * pad)));

        return new BatteryShapePlan(
            new PixelRect(x, y, bodyWidth, h),
            new PixelRect(x + bodyWidth, y + Math.Round((h - capHeight) / 2), cap, capHeight),
            stroke,
            corner,
            inner,
            Math.Round(inner.Height * 0.8));
    }

    /// <summary>
    /// How much of the inside the fill takes at a charge. Any charge above 0 is at least a sliver
    /// of the stroke's width, so 1% is not drawn as empty.
    /// </summary>
    public static double FillWidth(BatteryShapePlan plan, double fraction)
    {
        var share = double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);
        if (share <= 0 || plan.Inner.Width <= 0)
        {
            return 0;
        }

        return Math.Min(plan.Inner.Width, Math.Max(plan.Stroke, plan.Inner.Width * share));
    }
}

/// <summary>
/// [S-6263] The ping style's signal bars, as Windows draws a Wi-Fi signal: four bars rising left to
/// right, as many lit as the latency deserves.
/// </summary>
public static class PingSignal
{
    public const int BarCount = 4;

    /// <summary>
    /// The latencies, in milliseconds, at or under which an answer earns 4, 3 and 2 bars: up to 50 ms is
    /// as good as a connection gets for a call or a game, up to 100 ms is fine for anything but a
    /// fast game, up to 200 ms is felt in a call; past that one bar. An unanswered probe is none.
    /// </summary>
    public static readonly IReadOnlyList<double> Thresholds = [50, 100, 200];

    /// <summary>The bars' box: a little wider than tall, as the Wi-Fi glyph.</summary>
    public const double Aspect = 1.25;

    /// <summary>The gap between two bars as a share of a bar's width.</summary>
    public const double GapShare = 0.4;

    /// <summary>
    /// How many bars a probe earns: none for an unanswered one (null, NaN, negative), else 4 down
    /// to 1 by <see cref="Thresholds"/>.
    /// </summary>
    public static int Level(double? latencyMs)
    {
        if (latencyMs is not { } ms || double.IsNaN(ms) || ms < 0)
        {
            return 0;
        }

        var bars = BarCount;
        foreach (var threshold in Thresholds)
        {
            if (ms <= threshold)
            {
                return bars;
            }

            bars--;
        }

        return bars;
    }

    /// <summary>
    /// The bars in a box (fitted to <see cref="Aspect"/> and bottom-left aligned within it): bar
    /// <c>i</c> is <c>(i + 1) / 4</c> of the height, standing on the box's bottom.
    /// </summary>
    public static IReadOnlyList<PixelRect> Bars(double width, double height)
    {
        var (w, h) = PictureFace.Inside(width, height, Aspect);
        if (h <= 0)
        {
            return [];
        }

        var bar = w / (BarCount + ((BarCount - 1) * GapShare));
        var gap = bar * GapShare;
        var bars = new PixelRect[BarCount];

        for (var i = 0; i < BarCount; i++)
        {
            var barHeight = Math.Round(h * (i + 1) / BarCount);
            bars[i] = new PixelRect(Math.Round(i * (bar + gap)), h - barHeight, Math.Max(1, Math.Round(bar)), barHeight);
        }

        return bars;
    }
}

/// <summary>[S-6264] Two readings side by side or one above the other, and their step.</summary>
public readonly record struct NumbersPlan(bool Side, TypeStep Step);

/// <summary>
/// [S-6264] A style that is its figures alone (the network's download and upload): both the same
/// size, as large as the box allows, stacked or side by side, whichever draws them larger
/// (a tie stacks them). A box past Display grows them as a lone meter reading grows ([S-1762]).
/// </summary>
public static class MeterNumbers
{
    public static NumbersPlan Plan(WidgetArea box, HeroBound bound, bool icon, double gap)
    {
        var width = Math.Max(0, box.Width);
        var height = Math.Max(0, box.Height);

        var stacked = Step(width, (height - gap) / 2, bound, icon);
        var side = Step((width - gap) / 2, height, bound, icon);

        return side.FontSize > stacked.FontSize ? new NumbersPlan(true, side) : new NumbersPlan(false, stacked);
    }

    private static TypeStep Step(double width, double height, HeroBound bound, bool icon)
    {
        var step = HeroLayout.FitBeside(width, height, bound.Value, bound.Unit, FluentType.BodyStrong, FluentType.Display, icon);
        var room = icon ? width - HeroLayout.IconSize(step) - HeroLayout.IconGap : width;
        return HeroLayout.Grow(step, room, height, bound.Value, bound.Unit);
    }
}

/// <summary>[S-6266] A ring and the lines that go with it: beside or under, the ring's diameter and how many lines fit.</summary>
public readonly record struct RingFacePlan(bool Beside, double Diameter, int Lines);

/// <summary>
/// [S-6266] A ring style's face (the battery's charge, a drive's used share): the ring is the
/// must-show part and takes what is left; the lines (a drive's name, its space) come in priority
/// order where they fit, beside the ring on a wide strip, under it otherwise.
/// </summary>
public static class RingFace
{
    /// <summary>The least ring worth drawing: under it the figure inside no longer fits.</summary>
    public const double MinRing = 48;

    /// <summary>Beside the ring, the lines' column needs at least this width.</summary>
    public const double MinColumn = 120;

    public const double Gap = 12;

    public static RingFacePlan Plan(WidgetArea box, bool wide, IReadOnlyList<double> lineHeights, double lineGap)
    {
        ArgumentNullException.ThrowIfNull(lineHeights);
        var width = Math.Max(0, box.Width);
        var height = Math.Max(0, box.Height);

        if (wide && lineHeights.Count > 0)
        {
            var diameter = Math.Floor(Math.Min(height, width - MinColumn - Gap));
            if (diameter >= MinRing)
            {
                return new RingFacePlan(true, diameter, Lines(lineHeights, height, lineGap).Count);
            }
        }

        var (shown, spent) = Lines(lineHeights, height - MinRing - Gap, lineGap);
        spent += shown > 0 ? Gap : 0;
        return new RingFacePlan(false, Math.Floor(Math.Max(0, Math.Min(width, height - spent))), shown);
    }

    /// <summary>How many of the lines, in order, fit a height, and the height they take with their gaps.</summary>
    private static (int Count, double Height) Lines(IReadOnlyList<double> lineHeights, double room, double gap)
    {
        var used = 0.0;
        var count = 0;

        foreach (var line in lineHeights)
        {
            var cost = line + (count > 0 ? gap : 0);
            if (used + cost > room + Disclosure.Tolerance)
            {
                break;
            }

            used += cost;
            count++;
        }

        return (count, used);
    }

    /// <summary>
    /// The figure inside a ring of <paramref name="diameter"/> (its stroke <see cref="RingArc.Stroke"/>):
    /// the largest step from Caption whose line fits a chord through the hole, clear of the stroke.
    /// </summary>
    public static TypeStep Figure(double diameter, HeroBound bound)
    {
        var hole = Hole(diameter);
        return HeroLayout.Fit(hole, hole * 0.5, bound.Value, bound.Unit, FluentType.Caption, FluentType.Display);
    }

    /// <summary>The width a line inside a ring may take: the hole, a little in from the stroke.</summary>
    public static double Hole(double diameter)
        => Math.Floor(Math.Max(0, diameter - (2 * RingArc.Stroke(diameter))) * 0.86);
}
