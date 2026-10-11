namespace Themia.Core.Widgets;

/// <summary>[S-2402] How a detail widget draws one fact: a label and its value.</summary>
public enum FactStyle
{
    /// <summary>One line: the label on the left, the value on the right, both Body.</summary>
    Line = 0,

    /// <summary>A tile: the label in Caption over the value in Body Large.</summary>
    Tile = 1,

    /// <summary>A large tile: the label in Caption over the value in Title.</summary>
    LargeTile = 2,
}

/// <summary>[S-2402] Where the hero (the widget's main figure) sits.</summary>
public enum DetailArrangement
{
    /// <summary>The hero on top, the facts under it, a list (if any) under those.</summary>
    Stacked = 0,

    /// <summary>A wide strip: the hero in a column on the left, the facts beside it.</summary>
    Side = 1,

    /// <summary>A tile too small for anything else: the hero alone, filling it.</summary>
    HeroOnly = 2,
}

/// <summary>[S-2402] What a detail widget has to place, in its own terms.</summary>
/// <param name="Hero">Whether it has a main figure at all (a list widget may not).</param>
/// <param name="HeroCharacters">The widest the main figure can be, in characters ("3.42 GHz" is 8).</param>
/// <param name="Caption">Whether a line under the figure says what it is of ("of 31.7 GB").</param>
/// <param name="Facts">How many facts it has, most wanted first.</param>
/// <param name="ListRows">How many list rows it has (drives, processes, adapters); 0 for none.</param>
/// <param name="ListRowHeight">One list row, in DIPs.</param>
/// <param name="ValueCharacters">The longest fact value, in characters ("31 GB / 68 GB" is 13): a column never cuts one.</param>
/// <param name="LabelCharacters">The longest fact label, in characters.</param>
public readonly record struct DetailShape(bool Hero, double HeroCharacters, bool Caption, int Facts, int ListRows = 0, double ListRowHeight = 32, double ValueCharacters = 0, double LabelCharacters = 0);

/// <summary>[S-2402] One detail widget's face for a box.</summary>
/// <param name="Arrangement">Where the hero goes.</param>
/// <param name="Hero">The hero's step.</param>
/// <param name="Caption">Whether the caption line is shown.</param>
/// <param name="Style">How each fact is drawn.</param>
/// <param name="Columns">How many columns the facts are laid out in, row by row.</param>
/// <param name="Facts">How many facts are shown (the first ones).</param>
/// <param name="HeroColumn">The hero's column width on a strip (the whole width otherwise).</param>
/// <param name="ListHeight">The list's height; 0 when it is not shown.</param>
public readonly record struct DetailFacePlan(
    DetailArrangement Arrangement,
    TypeStep Hero,
    bool Caption,
    FactStyle Style,
    int Columns,
    int Facts,
    double HeroColumn,
    double ListHeight)
{
    public bool List => ListHeight > 0;
}

/// <summary>
/// [S-2402] The ladder every system detail widget grows by. A detail widget is a main figure (the
/// hero: the speed, the memory in use, the charge), a caption saying what it is of, a set of facts in
/// priority order, and for some a list (drives, processes, adapters). The tile shows more of that as
/// it grows, never by scaling one layout:
/// <list type="bullet">
/// <item>Compact both ways: the hero alone, filling the tile (a list widget: as many rows as fit).</item>
/// <item>A wide strip: the hero in a column on the left, the facts beside it in lines.</item>
/// <item>Otherwise stacked: hero, caption, then the facts. When every fact fits, the richest fact style
/// and the largest hero that still fit are chosen together — lines, then tiles, then large tiles —
/// so a large tile is a page of big figures rather than a few lines under empty space. When they do
/// not all fit, facts are lines and as many whole rows as fit are shown, most wanted first.</item>
/// <item>A list takes everything under the facts, at least one whole row, or is not shown.</item>
/// </list>
/// Costs are line heights of the live type ramp, so a larger text size sheds a rung rather than
/// overflowing. Pure: the arithmetic is pinned by tests.
/// </summary>
public static class DetailFace
{
    /// <summary>Space between two rows of line facts.</summary>
    public const double LineGap = 4;

    /// <summary>Space between two rows (and two columns) of tiles.</summary>
    public const double TileGap = 8;

    /// <summary>Space between two columns of line facts.</summary>
    public const double ColumnGap = 24;

    /// <summary>Space between the hero block and the facts, and between the facts and a list.</summary>
    public const double SectionGap = 12;

    /// <summary>The narrowest a column of line facts may be ("Logical processors 16").</summary>
    public const double MinLineColumn = 170;

    /// <summary>The narrowest a tile may be.</summary>
    public const double MinTileColumn = 120;

    /// <summary>The narrowest a large tile may be.</summary>
    public const double MinLargeTileColumn = 150;

    /// <summary>At most this many columns, whatever the width: past it a row is a sentence nobody reads.</summary>
    public const int MaxColumns = 4;

    /// <summary>On a strip, the hero's column takes this share of the width.</summary>
    public const double SideShare = 0.4;

    /// <summary>How far past Display the hero may grow into room the facts leave.</summary>
    public const double MaxHeroGrowth = 2.4;

    /// <summary>The height of one fact in a style, at the live text size.</summary>
    public static double FactHeight(FactStyle style) => style switch
    {
        FactStyle.LargeTile => FluentType.Caption.LineHeight + FluentType.Title.LineHeight,
        FactStyle.Tile => FluentType.Caption.LineHeight + FluentType.BodyLarge.LineHeight,
        _ => FluentType.Body.LineHeight,
    };

    /// <summary>The gap between two rows of facts in a style.</summary>
    public static double RowGap(FactStyle style) => style == FactStyle.Line ? LineGap : TileGap;

    /// <summary>The gap between two columns of facts in a style.</summary>
    public static double ColGap(FactStyle style) => style == FactStyle.Line ? ColumnGap : TileGap;

    /// <summary>How many columns of <paramref name="style"/> fit <paramref name="width"/>, never more than there are facts.</summary>
    public static int Columns(FactStyle style, double width, int facts, double valueChars = 0, double labelChars = 0)
    {
        if (facts <= 0 || !double.IsFinite(width) || width <= 0)
        {
            return 1;
        }

        var min = style switch
        {
            FactStyle.LargeTile => MinLargeTileColumn,
            FactStyle.Tile => MinTileColumn,
            _ => MinLineColumn,
        };

        min = Math.Max(min, ColumnNeed(style, valueChars, labelChars));

        var gap = ColGap(style);
        var fit = (int)Math.Floor((width + gap + Disclosure.Tolerance) / (min + gap));
        return Math.Clamp(Math.Min(fit, facts), 1, MaxColumns);
    }

    /// <summary>[S-2402] The width one column of <paramref name="style"/> needs so neither its label nor its value is cut.</summary>
    public static double ColumnNeed(FactStyle style, double valueChars, double labelChars) => style switch
    {
        FactStyle.LargeTile => Math.Max(FluentType.EstimateWidth(FluentType.Title, valueChars), FluentType.EstimateWidth(FluentType.Caption, labelChars)),
        FactStyle.Tile => Math.Max(FluentType.EstimateWidth(FluentType.BodyLarge, valueChars), FluentType.EstimateWidth(FluentType.Caption, labelChars)),
        _ => FluentType.EstimateWidth(FluentType.Body, valueChars + labelChars) + 8,
    };

    /// <summary>The height <paramref name="rows"/> rows of facts take.</summary>
    public static double FactsHeight(FactStyle style, int rows)
        => rows <= 0 ? 0 : (rows * FactHeight(style)) + ((rows - 1) * RowGap(style));

    /// <summary>
    /// [S-2402] The plan for <paramref name="area"/> (the card's content box, edge to edge).
    /// <paramref name="inset"/> is the content inset on every side.
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
        var height = Math.Max(0, area.Height - inset);
        var tiny = area.WidthClass == SizeClass.Compact && area.HeightClass == SizeClass.Compact;
        var list = shape.ListRows > 0 && shape.ListRowHeight > 0;

        // A tile small both ways says one thing: the hero, as big as the tile allows. A widget
        // without a hero gives the whole tile to its list instead.
        if (tiny && shape.Hero)
        {
            return new DetailFacePlan(DetailArrangement.HeroOnly, MeterFace.Reading(width, height, chars), false, FactStyle.Line, 1, 0, area.Width, 0);
        }

        if (tiny || (!shape.Hero && facts == 0))
        {
            return new DetailFacePlan(DetailArrangement.Stacked, FluentType.Subtitle, false, FactStyle.Line, 1, 0, area.Width, list ? ListHeight(area.Height, shape) : 0);
        }

        // A strip without a list: the hero on the left, the facts in lines beside it, as many whole
        // rows as the height holds.
        if (shape.Hero && !list && facts > 0 && area.Shape == AreaShape.Wide && area.WidthClass >= SizeClass.Expanded)
        {
            var column = Math.Round(Math.Max(area.Width * SideShare, FluentType.EstimateWidth(FluentType.Title, chars) + (2 * inset)));
            column = Math.Min(column, area.Width * 0.5);
            var heroWidth = Math.Max(0, column - (2 * inset));
            var heroBox = height - (shape.Caption ? FluentType.Body.LineHeight : 0);
            var hero = MeterFace.Reading(heroWidth, Math.Min(heroBox, FluentType.Display.LineHeight), chars);
            var caption = shape.Caption && hero.LineHeight + FluentType.Body.LineHeight <= height + Disclosure.Tolerance;
            var right = Math.Max(0, area.Width - column - inset);
            var cols = Columns(FactStyle.Line, right, facts, shape.ValueCharacters, shape.LabelCharacters);
            var rows = Disclosure.WholeRows(height, FactHeight(FactStyle.Line), LineGap);
            return new DetailFacePlan(DetailArrangement.Side, hero, caption, FactStyle.Line, cols, Math.Min(facts, rows * cols), column, 0);
        }

        var rowsTotal = facts;
        var captionHeight = shape.Hero && shape.Caption ? FluentType.Body.LineHeight : 0;
        var listMin = list ? SectionGap + shape.ListRowHeight : 0;

        // A list widget keeps its hero modest: the rows are the point.
        var ceiling = list ? FluentType.Title : FluentType.Display;
        TypeStep[] heroes = [FluentType.Display, FluentType.TitleLarge, FluentType.Title, FluentType.Subtitle];
        FactStyle[] styles = list ? [FactStyle.Line] : [FactStyle.LargeTile, FactStyle.Tile, FactStyle.Line];

        // Everything fits: the richest style, then the largest hero, that leave room for every fact.
        foreach (var style in styles)
        {
            // A tile whose value would be cut in a single column is not this tile's style.
            if (style != FactStyle.Line && ColumnNeed(style, shape.ValueCharacters, shape.LabelCharacters) > width + Disclosure.Tolerance)
            {
                continue;
            }

            var cols = Columns(style, width, facts, shape.ValueCharacters, shape.LabelCharacters);
            var rows = (rowsTotal + cols - 1) / Math.Max(1, cols);
            var factsHeight = FactsHeight(style, rows);

            foreach (var hero in heroes)
            {
                if (hero.FontSize > ceiling.FontSize || (shape.Hero && FluentType.EstimateWidth(hero, chars) > width + Disclosure.Tolerance))
                {
                    continue;
                }

                var need = (shape.Hero ? hero.LineHeight + captionHeight : 0)
                    + (shape.Hero && facts > 0 ? SectionGap : 0)
                    + factsHeight
                    + listMin;

                if (need <= height + Disclosure.Tolerance)
                {
                    var used = need - listMin;
                    var chosen = hero;

                    // No empty space at L / XL: without a list, the room every fact leaves goes to
                    // the hero (scaled past the ramp as the clock is, [S-641]), up to 2.4 × Display.
                    if (!list && shape.Hero && hero.FontSize >= FluentType.Display.FontSize)
                    {
                        var box = Math.Min(hero.LineHeight + (height - need), FluentType.Display.LineHeight * MaxHeroGrowth);
                        var grown = FluentType.Hero(width, box, chars);

                        if (grown.FontSize > hero.FontSize)
                        {
                            chosen = grown;
                        }
                    }

                    return new DetailFacePlan(DetailArrangement.Stacked, chosen, captionHeight > 0, style, cols, facts, area.Width,
                        list ? ListHeight(area.Height - used - SectionGap, shape) : 0);
                }

                if (!shape.Hero)
                {
                    break;
                }
            }
        }

        // Not everything fits: lines, the hero at the size that leaves the most for the facts, and
        // as many whole rows as fit, in priority order.
        var step = shape.Hero ? FluentType.Fit(width, height, chars, FluentType.Body, FluentType.Subtitle) : FluentType.Subtitle;
        var room = new Disclosure(width, height, SectionGap);

        if (shape.Hero)
        {
            room.Reserve(step.LineHeight + (captionHeight > 0 && step.LineHeight + captionHeight <= height ? captionHeight : 0));
        }

        var showCaption = captionHeight > 0 && step.LineHeight + captionHeight <= height + Disclosure.Tolerance;
        var lineCols = Columns(FactStyle.Line, width, facts, shape.ValueCharacters, shape.LabelCharacters);
        var lineRows = room.TakeRows(FactHeight(FactStyle.Line), (facts + lineCols - 1) / lineCols, LineGap);
        var shown = Math.Min(facts, lineRows * lineCols);
        var spent = height - room.Remaining;
        var listHeight = list ? ListHeight(area.Height - spent - SectionGap, shape) : 0;
        return new DetailFacePlan(DetailArrangement.Stacked, step, showCaption, FactStyle.Line, lineCols, shown, area.Width, listHeight);
    }

    /// <summary>The list's share of <paramref name="room"/>: all of it when one whole row fits, else none.</summary>
    private static double ListHeight(double room, DetailShape shape)
        => room + Disclosure.Tolerance >= shape.ListRowHeight ? Math.Max(0, room) : 0;
}
