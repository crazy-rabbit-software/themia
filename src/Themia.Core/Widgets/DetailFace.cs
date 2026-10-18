namespace Themia.Core.Widgets;

/// <summary>[S-2402] Where the hero (the widget's main figure) sits.</summary>
public enum DetailArrangement
{
    /// <summary>The hero on top, the facts under it, a list (if any) under those.</summary>
    Stacked = 0,

    /// <summary>A wide strip: the hero in a column on the left, the facts beside it.</summary>
    Side = 1,

    /// <summary>A tile too small for anything else: the hero alone.</summary>
    HeroOnly = 2,
}

/// <summary>[S-2402] What a detail widget has to place, in its own terms.</summary>
/// <param name="Hero">Whether it has a main figure at all (a list widget may not).</param>
/// <param name="HeroCharacters">The widest the main figure can be, in characters ("3.42 GHz" is 8), its unit counted at the number's size.</param>
/// <param name="Caption">Whether a line under the figure says what it is of ("of 31.7 GB").</param>
/// <param name="Facts">How many facts it has, most wanted first.</param>
/// <param name="ListRows">How many list rows it has (drives, processes, adapters); 0 for none.</param>
/// <param name="ListRowHeight">One list row, in DIPs.</param>
/// <param name="ValueCharacters">The longest fact value, in characters ("31 GB / 68 GB" is 13): a column never cuts one.</param>
/// <param name="LabelCharacters">The longest fact label, in characters.</param>
/// <param name="ListPadding">The list's own padding above and below its rows, counted in its natural height.</param>
/// <param name="RowsGrow">Whether the list's rows grow into the room the list has (a drive in a large tile, [S-2420]).</param>
public readonly record struct DetailShape(
    bool Hero,
    double HeroCharacters,
    bool Caption,
    int Facts,
    int ListRows = 0,
    double ListRowHeight = 32,
    double ValueCharacters = 0,
    double LabelCharacters = 0,
    double ListPadding = 0,
    bool RowsGrow = false);

/// <summary>[S-2402] One detail widget's face for a box.</summary>
/// <param name="Arrangement">Where the hero goes.</param>
/// <param name="Hero">The hero's step.</param>
/// <param name="Caption">Whether the caption line is shown.</param>
/// <param name="Style">How each fact is drawn.</param>
/// <param name="Columns">How many columns the facts are laid out in, row by row.</param>
/// <param name="Facts">How many facts are shown (the first ones).</param>
/// <param name="HeroColumn">The hero's column width on a strip (the whole width otherwise).</param>
/// <param name="ListHeight">The list's height; 0 when it is not shown.</param>
/// <param name="Top">[S-3861] The room above the stacked block, past the top inset: half of what nothing could use.</param>
public readonly record struct DetailFacePlan(
    DetailArrangement Arrangement,
    TypeStep Hero,
    bool Caption,
    FactStyle Style,
    int Columns,
    int Facts,
    double HeroColumn,
    double ListHeight,
    double Top = 0)
{
    public bool List => ListHeight > 0;
}

/// <summary>
/// [S-2402] The ladder every system detail widget grows by. A detail widget is a main figure (the
/// hero: the speed, the memory in use, the charge), a caption saying what it is of, a set of facts in
/// priority order, and for some a list (drives, processes, adapters). The tile shows more of that as
/// it grows, never by scaling one layout:
/// <list type="bullet">
/// <item>Compact both ways: the hero alone (a list widget: as many rows as fit).</item>
/// <item>A wide strip without a list: the hero in a column on the left, the facts beside it in lines.</item>
/// <item>Otherwise stacked, as one height budget: the hero at Subtitle with its caption is
/// reserved; the facts take whole rows in priority order, as stat cells when every one fits in two
/// or more columns of cells, else as stat lines ([S-3862]); a list takes one whole row or more
/// after them.</item>
/// </list>
/// [S-3861] What the rungs leave goes to the core content and then stays balanced, never a band
/// under the last part: the hero grows into it (to the tier's step, <see cref="HeroLayout.TierStep"/>;
/// to Display and past it, as the weather's does, once every fact is shown and there is no list);
/// a list is only as tall as its rows (a list whose rows grow takes all of it); what is still
/// left is split above and below the block, so it stands in the middle of the tile.
/// Costs are line heights of the live type ramp. Pure: the arithmetic is pinned by tests.
/// </summary>
public static class DetailFace
{
    /// <summary>Space between two rows (and two columns) of stat cells.</summary>
    public const double CellGap = 8;

    /// <summary>Space between two columns of stat lines.</summary>
    public const double ColumnGap = 24;

    /// <summary>Space between the hero block and the facts, and between the facts and a list.</summary>
    public const double SectionGap = 12;

    /// <summary>The narrowest a column of stat lines may be ("Logical processors 16").</summary>
    public const double MinLineColumn = 170;

    /// <summary>The narrowest a stat cell may be.</summary>
    public const double MinCellColumn = 120;

    /// <summary>At most this many columns, whatever the width: past it a row is a sentence nobody reads.</summary>
    public const int MaxColumns = 4;

    /// <summary>On a strip, the hero's column takes this share of the width.</summary>
    public const double SideShare = 0.4;

    /// <summary>The height of one fact in a style: the shared parts' own ([S-668]).</summary>
    public static double FactHeight(FactStyle style) => style == FactStyle.Cell ? PartMetrics.StatCell : PartMetrics.StatLine;

    /// <summary>The gap between two rows of facts: a stat line carries its own air.</summary>
    public static double RowGap(FactStyle style) => style == FactStyle.Cell ? CellGap : 0;

    /// <summary>The gap between two columns of facts in a style.</summary>
    public static double ColGap(FactStyle style) => style == FactStyle.Cell ? CellGap : ColumnGap;

    /// <summary>How many columns of <paramref name="style"/> fit <paramref name="width"/>, never more than there are facts.</summary>
    public static int Columns(FactStyle style, double width, int facts, double valueChars = 0, double labelChars = 0)
    {
        if (facts <= 0 || !double.IsFinite(width) || width <= 0)
        {
            return 1;
        }

        var min = Math.Max(style == FactStyle.Cell ? MinCellColumn : MinLineColumn, ColumnNeed(style, valueChars, labelChars));
        var gap = ColGap(style);
        var fit = (int)Math.Floor((width + gap + Disclosure.Tolerance) / (min + gap));
        return Math.Clamp(Math.Min(fit, facts), 1, MaxColumns);
    }

    /// <summary>[S-2402] The width one column of <paramref name="style"/> needs so neither its label nor its value is cut.</summary>
    public static double ColumnNeed(FactStyle style, double valueChars, double labelChars) => style == FactStyle.Cell
        ? Math.Max(FluentType.EstimateWidth(FluentType.BodyStrong, valueChars), FluentType.EstimateWidth(FluentType.Caption, labelChars))
        : FluentType.EstimateWidth(FluentType.Body, valueChars + labelChars) + 12;

    /// <summary>The height <paramref name="rows"/> rows of facts take.</summary>
    public static double FactsHeight(FactStyle style, int rows)
        => rows <= 0 ? 0 : (rows * FactHeight(style)) + ((rows - 1) * RowGap(style));

    /// <summary>
    /// [S-2402] The plan for <paramref name="area"/> (the card's content box, edge to edge).
    /// <paramref name="inset"/> is the content inset at the sides and the foot; the hero stands
    /// <see cref="HeroLayout.TopInset"/> under the header, as every hero readout does ([S-3860]).
    /// </summary>
    public static DetailFacePlan Plan(WidgetArea area, DetailShape shape, double inset)
    {
        var facts = Math.Max(0, shape.Facts);
        var chars = Math.Max(1, shape.HeroCharacters);

        if (!area.IsMeasured)
        {
            return new DetailFacePlan(DetailArrangement.Stacked, FluentType.Subtitle, false, FactStyle.Line, 1, 0, area.Width, 0);
        }

        var width = Math.Max(0, area.Width - (2 * inset));
        var height = Math.Max(0, area.Height - HeroLayout.TopInset - inset);
        var tiny = area.WidthClass == SizeClass.Compact && area.HeightClass == SizeClass.Compact;
        var list = shape.ListRows > 0 && shape.ListRowHeight > 0;
        var tier = HeroLayout.TierStep(area);

        // A tile small both ways says one thing: the hero, at the tier's step where it fits.
        if (tiny && shape.Hero)
        {
            return new DetailFacePlan(DetailArrangement.HeroOnly, Fit(width, height, chars, tier), false, FactStyle.Line, 1, 0, area.Width, 0);
        }

        // A widget without a hero is its list, read from the top: it takes the whole tile.
        if (!shape.Hero)
        {
            return new DetailFacePlan(DetailArrangement.Stacked, FluentType.Subtitle, false, FactStyle.Line, 1, 0, area.Width,
                list ? ListHeight(area.Height, shape) : 0);
        }

        // A strip without a list: the hero on the left, the facts in lines beside it, as many whole
        // rows as the height holds.
        if (!list && facts > 0 && area.Shape == AreaShape.Wide && area.WidthClass >= SizeClass.Expanded)
        {
            var column = Math.Round(Math.Max(area.Width * SideShare, FluentType.EstimateWidth(FluentType.Title, chars) + (2 * inset)));
            column = Math.Min(column, area.Width * 0.5);
            var heroWidth = Math.Max(0, column - (2 * inset));
            var captionLine = shape.Caption ? FluentType.Body.LineHeight : 0;
            var hero = Fit(heroWidth, height - captionLine, chars, tier);
            var caption = shape.Caption && hero.LineHeight + captionLine <= height + Disclosure.Tolerance;
            var right = Math.Max(0, area.Width - column - inset);
            var cols = Columns(FactStyle.Line, right, facts, shape.ValueCharacters, shape.LabelCharacters);
            var lines = Disclosure.WholeRows(height, FactHeight(FactStyle.Line));
            return new DetailFacePlan(DetailArrangement.Side, hero, caption, FactStyle.Line, cols, Math.Min(facts, lines * cols), column, 0);
        }

        // Rung 0: the hero at its smallest, and its caption where that fits too.
        var floor = FluentType.Subtitle;
        var showCaption = shape.Caption && floor.LineHeight + FluentType.Body.LineHeight <= height + Disclosure.Tolerance;
        var room = new Disclosure(width, height, SectionGap);
        room.Reserve(floor.LineHeight + (showCaption ? FluentType.Body.LineHeight : 0));

        // The facts: every one as cells where they fit in two columns or more, else as many whole
        // lines as fit, most wanted first.
        var style = FactStyle.Line;
        var columns = Columns(FactStyle.Line, width, facts, shape.ValueCharacters, shape.LabelCharacters);
        var shown = 0;

        if (facts > 0)
        {
            var cellColumns = Columns(FactStyle.Cell, width, facts, shape.ValueCharacters, shape.LabelCharacters);
            var cellRows = (facts + cellColumns - 1) / cellColumns;

            if (cellColumns >= 2
                && ColumnNeed(FactStyle.Cell, shape.ValueCharacters, shape.LabelCharacters) <= width + Disclosure.Tolerance
                && room.Fits(FactsHeight(FactStyle.Cell, cellRows)))
            {
                room.Take(FactsHeight(FactStyle.Cell, cellRows));
                style = FactStyle.Cell;
                columns = cellColumns;
                shown = facts;
            }
            else
            {
                var lineRows = room.TakeRows(FactHeight(FactStyle.Line), (facts + columns - 1) / columns, RowGap(FactStyle.Line));
                shown = Math.Min(facts, lineRows * columns);
            }
        }

        var left = room.Remaining;

        // The list: one whole row at least, then as tall as its rows (or all the room, for rows that
        // grow). It may run into the foot inset: its own padding is its inset.
        var listHeight = 0.0;
        var least = shape.ListRowHeight + shape.ListPadding;

        if (list && room.Take(least))
        {
            var available = least + room.Remaining + inset;
            var natural = (shape.ListRows * shape.ListRowHeight) + shape.ListPadding;
            listHeight = shape.RowsGrow ? available : Math.Min(natural, available);
            left = Math.Max(0, left - SectionGap - listHeight);
        }

        // [S-3861] What is left goes to the hero, then is split above and below the block.
        var heroRoom = floor.LineHeight + left;
        var everything = shown == facts && listHeight == 0;
        var step = Fit(width, heroRoom, chars, everything ? FluentType.Display : tier);

        if (everything)
        {
            step = HeroLayout.Grow(step, width, heroRoom, chars, 0);
        }

        // A list whose rows grow fills the tile and is read from the top; anything else stands in the middle.
        var spare = Math.Max(0, heroRoom - step.LineHeight);
        var top = shape.RowsGrow && listHeight > 0 ? 0 : Math.Floor(spare / 2);
        return new DetailFacePlan(DetailArrangement.Stacked, step, showCaption, style, Math.Max(1, columns), shown, area.Width, listHeight, top);
    }

    /// <summary>The hero's step: the shared readout's fit from Subtitle up to <paramref name="ceiling"/>.</summary>
    private static TypeStep Fit(double width, double height, double characters, TypeStep ceiling)
        => HeroLayout.Fit(width, height, characters, 0, FluentType.Subtitle, ceiling);

    /// <summary>The list's share of <paramref name="room"/>: all of it when one whole row fits, else none.</summary>
    private static double ListHeight(double room, DetailShape shape)
        => room + Disclosure.Tolerance >= shape.ListRowHeight ? Math.Max(0, room) : 0;
}
