namespace Themia.Core.Widgets;

/// <summary>
/// What the audio visualizer draws at one size. [S-3205]
/// </summary>
/// <param name="Horizontal">The bars lie across a tall column (growing from its start edge, bass at
/// the bottom) rather than standing on the floor of a tile or a strip.</param>
/// <param name="Bars">How many bars: a divisor of <see cref="Spectrum.Bands"/>, so each bar is a whole group of bands.</param>
/// <param name="Thickness">One bar's thickness along the long axis, DIPs.</param>
/// <param name="Gap">The space between two bars, DIPs.</param>
/// <param name="Inset">The padding kept from the card edge on every side, DIPs.</param>
/// <param name="Length">The room a bar has to grow in (the short axis of the box), DIPs.</param>
/// <param name="Floor">The idle baseline as a fraction of <see cref="Length"/>: a quiet band is a stub, never nothing.</param>
public readonly record struct VisualizerPlan(bool Horizontal, int Bars, double Thickness, double Gap, double Inset, double Length, double Floor);

/// <summary>
/// [S-3205] The audio visualizer's layout. The tile is the spectrum and nothing else, so the ladder
/// has one part and the size decides its grain: the bars run along the tile's long side, as many as
/// fit at <see cref="MinThickness"/> from <see cref="Counts"/>, and then they get thicker. The
/// analyser has <see cref="Spectrum.Bands"/> bands, so a larger tile draws the same bands larger
/// rather than inventing detail between them ([S-666]).
/// </summary>
public static class VisualizerLayout
{
    /// <summary>The bar counts, most first. Every one divides <see cref="Spectrum.Bands"/>.</summary>
    public static IReadOnlyList<int> Counts { get; } = [24, 12, 8, 6];

    /// <summary>A bar thinner than this reads as a hairline, not a bar.</summary>
    public const double MinThickness = 6;

    /// <summary>The idle baseline's height, DIPs: a flat line of stubs while nothing plays.</summary>
    public const double Baseline = 3;

    /// <summary>The padding on an XS tile; every larger tile keeps the card's 16.</summary>
    public const double CompactInset = 12;

    public const double Inset = 16;

    /// <summary>Plan the bars for the widget's content area (the card less its header).</summary>
    public static VisualizerPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new VisualizerPlan(false, Counts[^1], MinThickness, 2, Inset, 0, 1);
        }

        var inset = area.Least == SizeClass.Compact ? CompactInset : Inset;
        var box = area.Deflate(inset, inset);
        var horizontal = area.Shape == AreaShape.Tall;
        var along = horizontal ? box.Height : box.Width;
        var across = horizontal ? box.Width : box.Height;

        // The gap grows with the run, so a large tile does not read as a fence of thin slits.
        var gap = Math.Clamp(Math.Round(along / 100), 2, 6);
        var bars = Counts[^1];

        foreach (var count in Counts)
        {
            if (Thickness(along, count, gap) >= MinThickness)
            {
                bars = count;
                break;
            }
        }

        var floor = across > 0 ? Math.Min(1, Baseline / across) : 1;
        return new VisualizerPlan(horizontal, bars, Math.Max(0, Thickness(along, bars, gap)), gap, inset, across, floor);
    }

    private static double Thickness(double along, int count, double gap) => (along - (gap * (count - 1))) / count;

    /// <summary>
    /// Fold the analyser's bands into <paramref name="bars"/>: each bar is the loudest band of its
    /// group, so a kick that lights one band is not averaged away by its quiet neighbours. A level
    /// outside 0..1 (or not a number) is clamped, never drawn past the tile.
    /// </summary>
    public static void Group(ReadOnlySpan<float> bands, Span<float> bars)
    {
        if (bars.Length == 0)
        {
            return;
        }

        if (bands.Length == 0)
        {
            bars.Clear();
            return;
        }

        for (var i = 0; i < bars.Length; i++)
        {
            var low = i * bands.Length / bars.Length;
            var high = Math.Max(low + 1, (i + 1) * bands.Length / bars.Length);
            var peak = 0f;

            for (var b = low; b < high && b < bands.Length; b++)
            {
                var level = float.IsFinite(bands[b]) ? bands[b] : 0;
                peak = Math.Max(peak, level);
            }

            bars[i] = Math.Clamp(peak, 0, 1);
        }
    }
}
