using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-723] The standard style's face, on the detail widgets' plan ([S-2402], <see cref="DetailFace"/>):
/// the temperature as the Readout at the view's top, start-aligned, led by the sky's glyph at its step,
/// the condition as the line under it, on the step the widest reading takes (28, 40, 68 by the area,
/// never larger, [S-666]); the readings as fact rows (their 16 glyphs, the value primary), beside it in
/// a column of two fifths of the card on an expanded width, else 8 under it, only whole rows. Room left
/// at L and XL stays empty: no next days, no hours ([S-666]: the forecast widget's job).
/// </summary>
public static class WeatherNowLayout
{
    /// <summary>The widest reading the widget prints ("-18°F"), led by its glyph, its condition under it: the step does not jump with the reading.</summary>
    public static readonly ReadoutText Worst = new("-18", "°F", Lead: true, LineUnder: true);

    /// <summary>[S-723] The face for <paramref name="area"/> and <paramref name="facts"/> readings; "Show detail" off is the reading alone.</summary>
    public static DetailFacePlan Plan(WidgetArea area, int facts, bool details = true)
        => DetailFace.Plan(area, new DetailShape(Worst, details ? Math.Max(0, facts) : 0));
}
