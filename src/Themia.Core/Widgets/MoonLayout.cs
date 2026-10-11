namespace Themia.Core.Widgets;

/// <summary>[S-3105] One rung of the Moon widget's list, in the order the ladder gives them.</summary>
public enum MoonRow
{
    /// <summary>The next moonrise (the next of rise and set when only one row fits).</summary>
    Rise = 0,

    /// <summary>The next moonset.</summary>
    Set = 1,

    /// <summary>The next full or new moon, whichever comes first ([S-1491]).</summary>
    NextMajor = 2,
}

/// <summary>What the Moon widget shows at one size. [S-3105]</summary>
/// <param name="Disc">The disc's diameter, DIPs: never dropped, an even number from <see cref="MoonLayout.DiscFloor"/> up.</param>
/// <param name="Side">The text stands beside the disc (a wide strip) rather than under it.</param>
/// <param name="Name">The phase's name ("Waxing Crescent").</param>
/// <param name="Percent">How much of the disc is lit ("23%").</param>
/// <param name="Rows">How many rows of the list, from the top of <see cref="MoonLayout.Ladder"/>.</param>
public readonly record struct MoonPlan(double Disc, bool Side, bool Name, bool Percent, int Rows);

/// <summary>
/// [S-3105] The Moon widget's ladder, on one <see cref="Disclosure"/>: the disc (never dropped,
/// <see cref="DiscFloor"/>), the phase's name, the lit percentage, then whole list rows — moonrise,
/// moonset, the next full or new moon. Whatever the rungs leave, the disc takes, up to
/// <see cref="DiscCeiling"/>: a larger tile draws a larger moon, never a new part ([S-666]).
/// On a wide strip the text and rows stand beside the disc.
/// </summary>
public static class MoonLayout
{
    /// <summary>The smallest the disc is drawn.</summary>
    public const double DiscFloor = 24;

    /// <summary>The largest: past this a bigger moon says nothing more.</summary>
    public const double DiscCeiling = 192;

    /// <summary>The 16 DIP inset the disc and the text keep from the card edge (rows carry it as padding).</summary>
    public const double Inset = 16;

    /// <summary>The gap between the disc and the text beside or under it.</summary>
    public const double Gap = 8;

    /// <summary>The rows, in priority order.</summary>
    public static IReadOnlyList<MoonRow> Ladder { get; } = [MoonRow.Rise, MoonRow.Set, MoonRow.NextMajor];

    /// <summary>The step the phase's name is set in.</summary>
    public static TypeStep NameStep => FluentType.BodyStrong;

    /// <summary>The step the percentage is set in.</summary>
    public static TypeStep PercentStep => FluentType.Caption;

    /// <summary>
    /// Plan the widget for its content area.
    /// </summary>
    /// <param name="area">The content area (the card less its header).</param>
    /// <param name="rowHeight">One list row (Windows' 40, or 32 compact).</param>
    /// <param name="nameChars">The longest phase name in the current language, in characters.</param>
    public static MoonPlan Plan(WidgetArea area, double rowHeight, int nameChars)
    {
        if (!area.IsMeasured)
        {
            return new MoonPlan(DiscFloor, false, false, false, 0);
        }

        var width = Math.Max(0, area.Width - (2 * Inset));
        var height = Math.Max(0, area.Height - Gap);
        var nameWidth = FluentType.EstimateWidth(NameStep, nameChars);
        var side = area.Shape == AreaShape.Wide && area.WidthClass >= SizeClass.Regular;

        if (side)
        {
            // The disc as tall as the strip lets it be, up to two fifths of its width; the rest is
            // the text column, which climbs the same ladder from the full height.
            var disc = Even(Math.Clamp(Math.Min(height - Gap, width * 0.4), DiscFloor, DiscCeiling));
            var column = new Disclosure(Math.Max(0, area.Width - Inset - disc - (2 * Gap)), height);
            var name = column.Take(NameStep.LineHeight, nameWidth);
            var percent = column.Take(PercentStep.LineHeight);
            var rows = column.TakeRows(rowHeight, Ladder.Count);
            return new MoonPlan(disc, true, name, percent, rows);
        }

        var room = new Disclosure(width, height);
        room.Reserve(DiscFloor + Gap);
        var named = room.Take(NameStep.LineHeight, nameWidth);
        var lit = room.Take(PercentStep.LineHeight);
        var count = room.TakeRows(rowHeight, Ladder.Count);

        var grown = Even(Math.Clamp(Math.Min(DiscFloor + room.Remaining, width * 0.6), DiscFloor, DiscCeiling));
        return new MoonPlan(grown, false, named, lit, count);
    }

    /// <summary>Whole, even DIPs keep the disc's outline on whole pixels at 100%.</summary>
    private static double Even(double value) => Math.Max(DiscFloor, Math.Floor(value / 2) * 2);
}
