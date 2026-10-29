using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-3102] One rung of the Sun widget's rows, in the order the ladder gives them.</summary>
public enum SunRow
{
    /// <summary>Sunrise (the next of sunrise and sunset when only one row fits).</summary>
    Sunrise = 0,
    Sunset = 1,

    /// <summary>The daylight's length, its change since yesterday as its note ([S-1490]).</summary>
    Daylight = 2,

    /// <summary>Solar noon, the sun's height as its note.</summary>
    Noon = 3,
    Dawn = 4,
    Dusk = 5,
}

/// <summary>
/// [S-3102] What the Sun widget shows at one size, in the view's own DIPs (its top is the content's
/// top, under the header).
/// </summary>
/// <param name="GraphicWidth">The arc's or the day bar's box, from the inset (0: the times style draws none).</param>
/// <param name="GraphicHeight">Its height, from the view's top.</param>
/// <param name="Side">The rows stand beside the arc (an expanded width) rather than under it.</param>
/// <param name="RowsLeft">Where the rows start across (they carry the inset as their own padding).</param>
/// <param name="RowsTop">Where they start down: 8 under the graphic, or the top.</param>
/// <param name="Rows">How many rows, from the top of <see cref="SunLayout.Ladder"/>.</param>
/// <param name="Lines">How many lines each row shows (its pitch follows).</param>
public readonly record struct SunPlan(double GraphicWidth, double GraphicHeight, bool Side, double RowsLeft, double RowsTop, int Rows, int Lines);

/// <summary>
/// [S-3102] The Sun widget's ladder on the family's blocks ([S-6711], [S-6712]): the graphic (never
/// dropped) at the view's top in the inset, then whole fact rows in priority order, sunrise, sunset,
/// daylight, solar noon, dawn, dusk, 8 under it. The arc keeps <see cref="ArcFloor"/> and takes what
/// the rows leave, never taller than half its width (a stretched path says nothing more); the day bar
/// is one <see cref="Dot"/> tall, its track the Chart's 4. On an expanded width the arc stands in a
/// column of two fifths of the card and the rows beside it from the top, as the detail widgets'
/// figure does ([S-2402]); the bar, a wide thing, stays over its rows. The times style is the rows
/// alone. A row's lines come from the tier: two where it allows (the countdown, the note), else one.
/// Beside a graphic, rows need a regular width (180): a compact tile is the graphic alone.
/// </summary>
public static class SunLayout
{
    /// <summary>The arc's floor: below it the path is simply drawn smaller than it may be.</summary>
    public const double ArcFloor = FamilyShape.IconHuge;

    /// <summary>The sun's marker on the arc and on the bar, and the bar's height.</summary>
    public const double Dot = FamilySpace.S3;

    /// <summary>On an expanded width the arc's column takes this share of the card (its inset included).</summary>
    public const double ColumnShare = DetailFace.HeroShare;

    /// <summary>The rows, in priority order.</summary>
    public static IReadOnlyList<SunRow> Ladder { get; } = [SunRow.Sunrise, SunRow.Sunset, SunRow.Daylight, SunRow.Noon, SunRow.Dawn, SunRow.Dusk];

    /// <summary>Plan the widget for its view's area, its style and whether its rows are on.</summary>
    public static SunPlan Plan(WidgetArea area, SunStyle style, bool events)
    {
        var lines = RowRules.Lines(area, 2);
        if (!area.IsMeasured)
        {
            return new SunPlan(0, 0, false, 0, 0, 0, lines);
        }

        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var pitch = FamilyShape.RowHeight(lines);
        var graphic = style != SunStyle.Times;

        // [S-170] With the graphic gone the rows are forced on: a widget showing nothing is never the answer.
        // Beside a graphic a row needs a regular width (180): narrower, its name and time do not both fit.
        var rows = !graphic || (events && area.WidthClass >= SizeClass.Regular);

        if (!graphic)
        {
            return new SunPlan(0, 0, false, 0, 0, Math.Min(Ladder.Count, RowRules.Fit(height, lines)), lines);
        }

        var bar = style == SunStyle.DayBar;
        var floor = Math.Min(height, bar ? Dot : ArcFloor);

        if (rows && !bar && area.WidthClass >= SizeClass.Expanded)
        {
            var column = Math.Floor(area.Width * ColumnShare);
            var arcWidth = column - FamilySpace.Inset;
            return new SunPlan(arcWidth, Arc(floor, height, arcWidth), true, column, 0, Math.Min(Ladder.Count, RowRules.Fit(height, lines)), lines);
        }

        var count = rows ? Math.Min(Ladder.Count, RowRules.Fit(height - floor - FamilySpace.S2, lines)) : 0;
        var left = height - (count > 0 ? (count * pitch) + FamilySpace.S2 : 0);
        var tall = bar ? floor : Arc(floor, left, width);
        return new SunPlan(width, tall, false, 0, count > 0 ? tall + FamilySpace.S2 : 0, count, lines);
    }

    /// <summary>The arc's height: what it has, at most half its width, at least its floor.</summary>
    private static double Arc(double floor, double room, double width)
        => Math.Max(floor, Math.Min(room, Math.Floor(width / 2)));
}
