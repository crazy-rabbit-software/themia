namespace Themia.Core.Widgets;

/// <summary>What the large-icon style draws at one size ([S-6161]).</summary>
/// <param name="Stacked">The glyph above the temperature (a tall column), not beside it.</param>
/// <param name="IconSize">The sky's glyph, in DIPs.</param>
/// <param name="Temperature">The temperature's step.</param>
/// <param name="Condition">The condition ("Partly cloudy") under the temperature.</param>
/// <param name="HighLow">Today's high and low under the condition.</param>
public readonly record struct LargeIconPlan(bool Stacked, double IconSize, TypeStep Temperature, bool Condition, bool HighLow);

/// <summary>
/// [S-6161] [S-6162] The current conditions' other two styles, as pure functions of the slot. Both
/// keep the standard style's inset ([S-723]) and measure against the widest reading ("-18°C").
/// </summary>
public static class WeatherNowStyles
{
    /// <summary>The widest number the widget prints ("-18") and its unit ("°F"), in characters.</summary>
    public const double ValueCharacters = 3;

    public const double UnitCharacters = 2;

    /// <summary>Between the glyph and the temperature.</summary>
    public const double IconGap = 12;

    /// <summary>The glyph's floor; under it the style is a temperature with a bullet.</summary>
    public const double IconMin = 24;

    /// <summary>The glyph's ceiling: a large tile is a large reading, not a poster of a cloud.</summary>
    public const double IconMax = 200;

    /// <summary>
    /// [S-6161] The sky's glyph is the picture: as tall as the box beside the temperature, or on a
    /// tall column above it. The temperature takes the width left (Subtitle to Display); the
    /// condition, then today's range, follow under it where the height has a line for them and the
    /// width the condition's words (<paramref name="conditionCharacters"/>): never cut short.
    /// </summary>
    public static LargeIconPlan LargeIcon(WidgetArea slot, bool hasToday, int conditionCharacters = 14)
    {
        var box = slot.Deflate(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        var floor = HeroLayout.ValueWidth(FluentType.Subtitle, ValueCharacters, UnitCharacters);

        if (!slot.IsMeasured || box.Width <= 0 || box.Height <= 0)
        {
            return new LargeIconPlan(false, IconMin, FluentType.Subtitle, false, false);
        }

        if (slot.Shape == AreaShape.Tall)
        {
            // A column: the glyph across most of the width, the reading and its lines under it.
            var lines = new Disclosure(box.Width, box.Height, WeatherNowLayout.Gap);
            lines.Reserve(FluentType.Subtitle.LineHeight);
            var conditionUnder = lines.Take(FluentType.Body.LineHeight, FluentType.EstimateWidth(FluentType.Body, conditionCharacters));
            var rangeUnder = hasToday && conditionUnder && lines.Take(FluentType.Caption.LineHeight);
            var linesHeight = (conditionUnder ? FluentType.Body.LineHeight : 0) + (rangeUnder ? FluentType.Caption.LineHeight : 0);
            var reading = box.Height - linesHeight;
            var tallIcon = Math.Clamp(Math.Min(box.Width * 0.7, (reading - WeatherNowLayout.Gap) * 0.6), IconMin, IconMax);
            var tallStep = Step(box.Width, Math.Max(FluentType.Subtitle.LineHeight, reading - tallIcon - WeatherNowLayout.Gap));
            return new LargeIconPlan(true, Math.Floor(tallIcon), tallStep, conditionUnder, rangeUnder);
        }

        // Beside: the glyph as tall as the box (within its share of the width), the text in the rest.
        var icon = Math.Clamp(Math.Min(box.Height, box.Width * 0.42), IconMin, IconMax);
        if (box.Width - icon - IconGap < floor)
        {
            icon = Math.Max(IconMin, box.Width - IconGap - floor);
        }

        var text = Math.Max(0, box.Width - icon - IconGap);
        var room = new Disclosure(text, box.Height, 0);
        room.Reserve(FluentType.Subtitle.LineHeight);
        var condition = room.Take(FluentType.Body.LineHeight, FluentType.EstimateWidth(FluentType.Body, conditionCharacters));
        var highLow = hasToday && condition && room.Take(FluentType.Caption.LineHeight);
        var step = Step(text, FluentType.Subtitle.LineHeight + room.Remaining);
        return new LargeIconPlan(false, Math.Floor(icon), step, condition, highLow);
    }

    /// <summary>
    /// [S-6162] [S-659] The temperature alone, as large as the box holds it: the ramp while it is
    /// small, then filled past Display, as the minimal clock fills its tile ([S-6014]).
    /// </summary>
    public static TypeStep Minimal(WidgetArea slot)
    {
        var box = slot.Deflate(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        if (!slot.IsMeasured || box.Width <= 0 || box.Height <= 0)
        {
            return FluentType.Subtitle;
        }

        var fitted = HeroLayout.Fit(box.Width, box.Height, ValueCharacters, UnitCharacters, FluentType.Body, FluentType.Display);
        return HeroLayout.Fill(fitted, box.Width, box.Height, ValueCharacters, UnitCharacters, icon: false, beside: 0);
    }

    /// <summary>[S-659] The temperature's step in a box: the ramp to Display, then filled past it.</summary>
    private static TypeStep Step(double width, double height)
    {
        var step = HeroLayout.Fit(width, height, ValueCharacters, UnitCharacters, FluentType.Subtitle, FluentType.Display);
        return HeroLayout.Fill(step, width, height, ValueCharacters, UnitCharacters, icon: false, beside: 0);
    }
}
