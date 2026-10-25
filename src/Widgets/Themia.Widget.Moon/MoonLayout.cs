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
    /// <param name="rowWords">
    /// The widest a row's label and value get in the current language (their short forms, the gap
    /// between them), DIPs. A row is shown only where they fit beside its glyph.
    /// </param>
    public static MoonPlan Plan(WidgetArea area, double rowHeight, int nameChars, double rowWords = 0)
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
            // The text column climbs the ladder with the disc at its floor; the disc then takes what
            // the parts it showed leave: as tall as the strip lets it be, up to two fifths of its
            // width, never so wide that the name or a row's words no longer fit beside it.
            var column = new Disclosure(Math.Max(0, ColumnBeside(area, DiscFloor)), height);
            var name = column.Take(NameStep.LineHeight, nameWidth);
            var percent = column.Take(PercentStep.LineHeight);
            var rows = RowRoom(area, DiscFloor, true) >= rowWords ? column.TakeRows(rowHeight, Ladder.Count) : 0;

            var cap = Math.Min(height - Gap, width * 0.4);
            if (name)
            {
                cap = Math.Min(cap, area.Width - Inset - (2 * Gap) - nameWidth);
            }

            if (rows > 0)
            {
                cap = Math.Min(cap, area.Width - (2 * Inset) - Gap - RowChrome - rowWords);
            }

            return new MoonPlan(Even(Math.Clamp(cap, DiscFloor, DiscCeiling)), true, name, percent, rows);
        }

        var room = new Disclosure(width, height);
        room.Reserve(DiscFloor + Gap);
        var named = room.Take(NameStep.LineHeight, nameWidth);
        var lit = room.Take(PercentStep.LineHeight);
        var count = RowRoom(area, 0, false) >= rowWords ? room.TakeRows(rowHeight, Ladder.Count) : 0;

        var grown = Even(Math.Clamp(Math.Min(DiscFloor + room.Remaining, width * 0.6), DiscFloor, DiscCeiling));
        return new MoonPlan(grown, false, named, lit, count);
    }

    /// <summary>
    /// A row's chrome beside its words: its padding at both ends (<see cref="Inset"/>), the 16 DIP
    /// glyph and the 12 DIP gap after it.
    /// </summary>
    public const double RowChrome = (2 * Inset) + 16 + 12;

    /// <summary>What a row of this plan has for its label, note and value, DIPs.</summary>
    public static double RowRoom(WidgetArea area, MoonPlan plan) => RowRoom(area, plan.Disc, plan.Side);

    /// <summary>
    /// A row's room: the card's width under the disc; beside it, the width the disc and its margins
    /// leave, as the rows run to the card's right edge. Less the row's chrome either way.
    /// </summary>
    private static double RowRoom(WidgetArea area, double disc, bool side)
        => (side ? area.Width - (2 * Inset) - disc - Gap : area.Width) - RowChrome;

    /// <summary>The text column beside a disc of this size.</summary>
    private static double ColumnBeside(WidgetArea area, double disc) => area.Width - Inset - disc - (2 * Gap);

    /// <summary>Whole, even DIPs keep the disc's outline on whole pixels at 100%.</summary>
    private static double Even(double value) => Math.Max(DiscFloor, Math.Floor(value / 2) * 2);
}
