namespace Themia.Core.Widgets;

/// <summary>
/// [S-667] What is left of the hero readout's metrics: a value's unit step and the fit of a figure
/// and its unit into a box, which the ring's figure still uses (<see cref="RingFace.Figure"/>). The
/// readouts themselves are the family's Readout block ([S-6702]); the weather, the price, the rate
/// and the air quality left this ladder with group G3, and its glyph, token, growth, fill and tier
/// steps went with them.
/// </summary>
public static class HeroLayout
{
    /// <summary>The unit's size as a share of the value's: Windows' own readouts set it at about half.</summary>
    private const double UnitShare = 0.45;

    /// <summary>
    /// The step the unit is written in: the largest ramp step no bigger than <see cref="UnitShare"/>
    /// of the value's size, never below Caption, never as large as the value. Regular weight: the
    /// unit qualifies the number and must not compete with it.
    /// </summary>
    public static TypeStep UnitStep(TypeStep value)
    {
        // [S-659] Past the ramp the unit grows with the value from Title (Display's unit), so a
        // filled reading's "°C" does not stay a 28px footnote beside a 200px number.
        if (value.FontSize > FluentType.Display.FontSize)
        {
            return FluentType.Scaled(FluentType.Title, value.FontSize / FluentType.Display.FontSize) with { SemiBold = false };
        }

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
}

/// <summary>
/// [S-3860] The widest reading a hero is laid out for, in characters of each run as the readout
/// draws it: the number at the value's step, the unit (with its space) at <see cref="HeroLayout.UnitStep"/>.
/// A bound, never the live text, so the number does not change size between two samples.
/// </summary>
public readonly record struct HeroBound(double Value, double Unit)
{
    /// <summary>The bound of a worst-case formatted reading ("100%", "1023 KB/s"), split as the readout splits it.</summary>
    public static HeroBound Of(string worstCase)
    {
        var text = Themia.Sdk.Kit.ReadoutRules.Split(worstCase);
        return new HeroBound(text.Value.Length, text.Unit.Length);
    }
}
