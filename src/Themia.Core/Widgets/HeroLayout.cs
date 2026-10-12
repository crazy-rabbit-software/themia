namespace Themia.Core.Widgets;

/// <summary>
/// [S-667] The shared hero readout's metrics: the one big value a widget is about (a temperature,
/// a price), its unit, the short token beside it (a change) and the caption lines under it.
/// <para>
/// Weather, Stocks and the clock each grew their own: the unit at the value's full size ("14°C"
/// as one 68px word), a change bottom-aligned against a taller line so its baseline sat a few
/// pixels off the price's, a glyph sized by a different factor per widget. One set of rules here,
/// one control in the app (<c>HeroReadout</c>), so a temperature and a price on the same desktop
/// read as the same kind of thing.
/// </para>
/// </summary>
public static class HeroLayout
{
    /// <summary>Between the leading glyph and the value.</summary>
    public const double IconGap = 12;

    /// <summary>Between the value line and what stands beside it (a change, a condition).</summary>
    public const double BesideGap = 12;

    /// <summary>The unit's size as a share of the value's: Windows' own readouts set it at about half.</summary>
    private const double UnitShare = 0.45;

    /// <summary>
    /// The step the unit is written in: the largest ramp step no bigger than <see cref="UnitShare"/>
    /// of the value's size, never below Caption, never as large as the value. Regular weight: the
    /// unit qualifies the number and must not compete with it.
    /// </summary>
    public static TypeStep UnitStep(TypeStep value)
    {
        var wanted = value.FontSize * UnitShare;
        var chosen = FluentType.Caption;

        foreach (var step in FluentType.Ramp)
        {
            if (step.FontSize <= wanted && step.FontSize < value.FontSize)
            {
                chosen = step;
            }
        }

        return chosen with { SemiBold = false };
    }

    /// <summary>
    /// The step of the token beside the value (a change): Body next to a small value, BodyLarge
    /// once the value is a Title or larger, where 14px reads as a footnote.
    /// </summary>
    public static TypeStep TrailingStep(TypeStep value)
        => value.FontSize >= FluentType.Title.FontSize ? FluentType.BodyLarge : FluentType.Body;

    /// <summary>The leading glyph's size: it grows with the value, from 24 to 72, so it reads as the condition and not a bullet.</summary>
    public static double IconSize(TypeStep value)
        => Math.Clamp(value.FontSize * 0.8, 24, 72);

    /// <summary>
    /// How wide the value and its unit are at a step: the value's characters at the step, the
    /// unit's at <see cref="UnitStep"/>. The same estimate <see cref="FluentType.Fit"/> uses.
    /// </summary>
    public static double ValueWidth(TypeStep value, double valueCharacters, double unitCharacters)
        => FluentType.EstimateWidth(value, valueCharacters)
            + (unitCharacters > 0 ? FluentType.EstimateWidth(UnitStep(value), unitCharacters) : 0);

    /// <summary>
    /// The largest step from <paramref name="floor"/> to <paramref name="ceiling"/> at which the
    /// value and its unit fit <paramref name="width"/> and the value line fits <paramref name="height"/>.
    /// </summary>
    public static TypeStep Fit(double width, double height, double valueCharacters, double unitCharacters, TypeStep floor, TypeStep ceiling)
    {
        if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(height) || height <= 0)
        {
            return floor;
        }

        var chosen = floor;

        foreach (var step in FluentType.Ramp)
        {
            if (step.FontSize < floor.FontSize || step.FontSize > ceiling.FontSize)
            {
                continue;
            }

            if (step.LineHeight <= height && ValueWidth(step, valueCharacters, unitCharacters) <= width)
            {
                chosen = step;
            }
        }

        return chosen;
    }

    /// <summary>The most a value grows past Display: a large tile's reading, not a poster.</summary>
    public const double MaxGrowth = 1.5;

    /// <summary>
    /// Past the ramp: once <paramref name="step"/> is Display and the box has room for more, the
    /// value scales up (Display's own line-height ratio, as <see cref="FluentType.Hero"/> does for
    /// the clock) to at most <see cref="MaxGrowth"/>, so an XL tile is a large reading rather than
    /// a Display number in a field of nothing. Any other step comes back unchanged.
    /// </summary>
    public static TypeStep Grow(TypeStep step, double width, double height, double valueCharacters, double unitCharacters)
    {
        if (step.FontSize < FluentType.Display.FontSize || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return step;
        }

        var byWidth = width / ValueWidth(FluentType.Display, valueCharacters, unitCharacters);
        var byHeight = height / FluentType.Display.LineHeight;
        var factor = Math.Min(MaxGrowth, Math.Min(byWidth, byHeight));
        return factor > 1 ? FluentType.Scaled(FluentType.Display, factor) : FluentType.Display;
    }
}
