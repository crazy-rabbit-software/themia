using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-11540]..[S-11546] The calendar's sizes, each style its own and from the config alone, on the
/// family's tokens (inset 12, the period's line 24 over every style but the agenda, [S-12481], rows 28/40): what a style
/// shows is its settings'; how many of the same item (a week's days, a timeline's hours, the chosen day's rows, a day
/// cell's chip lines) is what the card holds ([S-14410], [S-14502]). Every axis takes every grid cell ([S-14500]). Pure.
/// </summary>
public static class CalendarSizing
{
    public const int DefaultAgendaRows = 6;

    /// <summary>[S-569] [S-12481] [S-12834] The period's line (a tool line) and the gap under it, over every style but the agenda: its whole-cell band.</summary>
    public static double NavLine => LineRules.NaturalBand;

    /// <summary>[S-12481] Where a style with the period's line starts its drawing: the card's inset and the line.</summary>
    public static double ContentTop => FamilySpace.ContentTop + NavLine;

    /// <summary>
    /// [S-569] [S-12834] The period's line's band from the card's top edge: the card's inset, the line and the gap under it,
    /// whole grid cells. The year, the day and the day strip lay their items from its foot as from a card's top edge.
    /// </summary>
    public static double LineBand => ItemSize.Optimal(LineRules.BandOver, ItemGrid.Default());

    /// <summary>[S-569] A style's own sizing with the period's line over it: every height its band taller.</summary>
    public static WidgetSizing Lined(WidgetSizing face)
    {
        ArgumentNullException.ThrowIfNull(face);
        static SizeDip Taller(SizeDip size) => new(size.Width, size.Height + LineBand);
        return face with { Min = Taller(face.Min), Natural = Taller(face.Natural), Max = Taller(face.Max) };
    }

    public const int DefaultWeekRows = 3;

    public const int DefaultHours = 8;

    public const int MinHours = 4;

    public const int MaxHours = 16;

    public const int MaxWeekRows = 12;

    /// <summary>[S-11540] The agenda's widths: a row's start, title and colour bar read from 260.</summary>
    public static KitWidths AgendaWidths { get; } = new(260, 300, 520);

    public static int ClampHours(int hours) => Math.Clamp(hours, MinHours, MaxHours);

    public static int ClampWeekRows(int rows) => Math.Clamp(rows, 1, MaxWeekRows);

    /// <summary>[S-11540] The agenda's rows as drawn: the config's rows and lines.</summary>
    public static KitRowsChoice AgendaShown(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new KitRowsChoice(KitSizing.ClampRows(config.Rows), KitSizing.ClampLines(config.Lines));
    }

    // ---- [S-11542] year ------------------------------------------------------------------------

    /// <summary>[S-11542] A small year's day: a square.</summary>
    public const double YearSquare = FamilySpace.S2;

    /// <summary>
    /// [S-15780] The padding inside a month's cell round its small month: the cells stand edge to edge (WR-3), the inset's
    /// place is theirs.
    /// </summary>
    public const double YearPad = FamilySpace.S2;

    /// <summary>[S-11542] What a year draws: always twelve months (a new one 4 × 3); squares, or with <paramref name="numbers"/> the numbers under the weekday letters.</summary>
    public static YearFit Year(bool numbers)
        => numbers
            ? new YearFit(12, 4, 3, FamilyType.Caption.LineHeight, true, true)
            : new YearFit(12, 4, 3, YearSquare, false, false);

    /// <summary>[S-11542] A small month at its own size: its name over its six weeks (and the weekday letters with numbers).</summary>
    public static SizeDip YearMonth(YearFit fit)
    {
        ArgumentNullException.ThrowIfNull(fit);
        return new SizeDip(7 * fit.DayCell, FamilyType.Caption.LineHeight + ((fit.Weekdays ? 7 : 6) * fit.DayCell));
    }

    /// <summary>[S-15780] A month's cell, its one size: its small month and the padding round it, whole grid cells.</summary>
    public static SizeDip YearCell(YearFit fit)
    {
        var month = YearMonth(fit);
        var grid = ItemGrid.Default();
        return new SizeDip(ItemSize.Optimal(month.Width + (2 * YearPad), grid), ItemSize.Optimal(month.Height + (2 * YearPad), grid));
    }

    /// <summary>
    /// [S-11542] [S-15780] The year at a new card's size: its months' cells 4 × 3, touching from the card's top edge. The
    /// card's size is the user's (no Size setting, [S-2449] retired): a larger card shows more of the months a line
    /// (<see cref="YearCells"/>), never larger months.
    /// </summary>
    public static WidgetSizing YearSizing(bool numbers)
    {
        var fit = Year(numbers);
        var cell = YearCell(fit);

        // [S-15789] Its least is one month's cell (WR-14: "a list's least is one item").
        return WidgetSizing.Range(cell, new SizeDip(fit.Columns * cell.Width, fit.Rows * cell.Height), new SizeDip(fit.Columns * cell.Width, fit.Rows * cell.Height));
    }

    /// <summary>
    /// [S-15780] The twelve months in a box <paramref name="width"/> × <paramref name="height"/> (the card's, from its top
    /// edge) on <paramref name="grid"/> (docs/WIDGET-RULES.md WR-4, WR-6; replaces [S-15672], whose months shared the card and
    /// grew with it: WR-9's grid of the card is the month's alone): each month's cell its one size (its small month and the
    /// padding round it in whole grid cells) at every size of the card, the cells touching from the card's left and top edges,
    /// as many a line as the width holds whole (1..12, [S-16438]: no month in a line's middle cut by the right edge while
    /// later months stand under it, WR-4), the months in reading order on as many lines as they take, the last line the
    /// card reaches cut by its foot.
    /// </summary>
    public static CellReach YearCells(YearFit fit, double width, double height, ItemGrid grid)
    {
        ArgumentNullException.ThrowIfNull(fit);
        var month = YearMonth(fit);
        return ItemSize.Wrapped(width, height, month.Width + (2 * YearPad), month.Height + (2 * YearPad), grid, 1, fit.Months);
    }

    /// <summary>The ways twelve months stand in columns and lines, a new card's first: the first that draws them largest wins.</summary>
    private static readonly (int Columns, int Rows)[] YearShapes = [(4, 3), (3, 4), (6, 2), (2, 6), (12, 1), (1, 12)];

    /// <summary>
    /// [S-16446] How the twelve months share a box <paramref name="width"/> × <paramref name="height"/> (the card's, from its
    /// top edge) where the card holds all twelve at their own size (WR-9: "the calendar does not follow this design
    /// philosophy"; "Bigger numbers too"; the re-audit of 2026-11-23: the months stood 56 DIP at every size, 417 DIP empty
    /// under them at 520 × 600): the year is a grid of the card, as the month is, its months' cells sharing it edge to edge in
    /// the columns and lines that draw the small months largest (a wide card 6 × 2 or 12 × 1, a tall one 3 × 4; a tie keeps a
    /// new card's 4 × 3), each small month scaled up as one in its cell inside its padding (<see cref="YearShare.Factor"/>).
    /// A card that does not hold all twelve at their own size (a factor under one) keeps the wrapped cells of
    /// <see cref="YearCells"/>, the last line cut by the foot.
    /// </summary>
    public static YearShare YearShares(YearFit fit, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(fit);
        var month = YearMonth(fit);
        var best = new YearShare(fit.Columns, fit.Rows, double.NegativeInfinity);
        foreach (var (columns, rows) in YearShapes)
        {
            var factor = Math.Min(((width / columns) - (2 * YearPad)) / month.Width, ((height / rows) - (2 * YearPad)) / month.Height);
            if (double.IsFinite(factor) && factor > best.Factor + 1e-9)
            {
                best = new YearShare(columns, rows, factor);
            }
        }

        return best with { Factor = double.IsFinite(best.Factor) ? best.Factor : 0 };
    }

    // ---- [S-11543] day --------------------------------------------------------------------------------

    /// <summary>[S-11543] An hour of a timeline the config sizes: a row of the family's.</summary>
    public const double Hour = FamilyShape.Row1;

    /// <summary>[S-11543] The day's band: one line, its item's own bar, or chips shared among several; none on a day without.</summary>
    public static DayBandFit DayBand(int items)
        => items <= 0 ? new DayBandFit(0, 0, false) : new DayBandFit(1, CalendarFit.DayBar, items > 1);

    /// <summary>[S-15671] A day's hours: a timeline never shows more.</summary>
    public const int HoursInDay = 24;

    /// <summary>
    /// [S-11543] [S-14502] [S-14810] [S-15671] The hours a timeline <paramref name="available"/> tall shows: every one it
    /// reaches at an hour's one fixed size (a row's optimal, whole cells), the last cut by the card's foot,
    /// <see cref="MinHours"/>..<see cref="HoursInDay"/> (the hours follow the card: a taller one shows more of the day).
    /// </summary>
    public static int HoursIn(double available, ItemGrid grid) => ItemSize.Count(available, Hour, grid, 1, HoursInDay);

    /// <summary>
    /// [S-15671] The day's hour rows in a room <paramref name="available"/> tall, from the first row's top to the card's foot:
    /// each an item at an hour's one fixed size, touching, every one the room reaches (<see cref="HoursIn"/>), the last cut by
    /// the foot, every edge on a whole device pixel.
    /// </summary>
    public static RowReach DayHours(double available, ItemGrid grid)
    {
        var room = double.IsFinite(available) ? Math.Max(0, available) : 0;
        return new RowReach(ItemSize.Optimal(Hour, grid), HoursIn(room, grid), grid.Raster) { Room = room };
    }

    /// <summary>
    /// [S-15671] [S-15789] The day's band row over its hours: a row of the day's one size (an hour's, whole cells on
    /// <paramref name="grid"/>, WR-6) on the card's top edge, its line centred in it, the inset its own padding; taller only
    /// where its lines need it; none without an all-day item.
    /// </summary>
    public static double DayBandRow(DayBandFit band, ItemGrid grid)
    {
        ArgumentNullException.ThrowIfNull(band);
        return band.Lines > 0 ? ItemSize.Optimal(Math.Max(Hour, band.Lines * band.LineHeight), grid) : 0;
    }

    /// <summary>[S-11543] [S-15671] The day's height over its hours: its band row on the card's top edge (the hours run to the foot).</summary>
    public static double DayOver => DayBandRow(DayBand(1), ItemGrid.Default());

    /// <summary>
    /// [S-11543] [S-14500] [S-15789] The day: 240..480 wide (a new one 300), its height <see cref="DefaultHours"/> hours, its
    /// least one (WR-14: "a list's least is one item"), at most 16 a new card's largest.
    /// </summary>
    public static WidgetSizing DaySizing()
    {
        var hour = ItemSize.Optimal(Hour, ItemGrid.Default());
        return WidgetSizing.Range(new(240, DayOver + hour), new(300, DayOver + (DefaultHours * hour)), new(480, DayOver + (MaxHours * hour)));
    }

    // ---- [S-11544] month ------------------------------------------------------------------------------

    /// <summary>[S-11544] The weeks a month shows: six, so every month is one height (its own weeks and the next days, dimmed).</summary>
    public const int MonthWeeks = 6;

    /// <summary>[S-11547] A day cell with titles: its padding and its number's line, over which the chip lines go.</summary>
    public static double MonthNumberLine => CalendarFit.MonthCellChrome + FamilyType.Caption.LineHeight;

    /// <summary>[S-11547] A column of the month with titles: at least this wide (a title's first letters).</summary>
    public const double MonthTitleColumn = 56;

    /// <summary>[S-11547] [S-16448] A new month with titles' column: wide enough for a chip that reads (<see cref="CalendarFit.ChipReadsFrom"/>).</summary>
    public const double MonthNaturalColumn = CalendarFit.ChipReadsFrom;

    /// <summary>[S-11547] The widest a month with titles' column grows.</summary>
    public const double MonthWidestColumn = 180;

    /// <summary>[S-11544] [S-11547] A month cell's height: its number over the config's chip lines, or over a dot (a row of the family's).</summary>
    public static double MonthCell(MonthEvents events, int lines = MonthPlan.DefaultLines)
        => events == MonthEvents.Chips
            ? MonthNumberLine + (MonthPlan.Lines(lines) * CalendarFit.ChipLine)
            : FamilyShape.Row1;

    /// <summary>[S-11544] [S-11547] What a month grid <paramref name="innerWidth"/> wide (the card less its inset) draws: six weeks, the config's look and lines.</summary>
    public static MonthFit Month(MonthEvents events, double innerWidth, int lines = MonthPlan.DefaultLines)
    {
        var column = Math.Max(0, innerWidth) / 7;
        var chips = events == MonthEvents.Chips;
        return new MonthFit(MonthWeeks, CalendarFit.Weekdays(column), FamilyType.Caption.LineHeight, MonthCell(events, lines),
            chips ? MonthDensity.Chips : MonthDensity.Dots, chips ? MonthPlan.Lines(lines) : 0, CalendarFit.ChipLine, chips && column >= CalendarFit.ChipTimeFrom, false);
    }

    /// <summary>
    /// [S-12832] [S-14101] [S-14500] What stands over a month's weeks, each part whole grid cells from the card's top edge
    /// as the month lays it (<see cref="MonthLayouts.Plan"/>): the period's line with the card's inset over it, the weekday
    /// line.
    /// </summary>
    public static double MonthOver
        => ItemSize.Optimal(LineRules.BandOver, ItemGrid.Default()) + ItemSize.Optimal(FamilyType.Caption.LineHeight, ItemGrid.Default());

    /// <summary>
    /// [S-11544] The month with dots: seven columns of 260 / 7 at least (a new one the least), its weekday letters and six
    /// weeks of 28 at least. [S-11547] With titles: seven columns of 56 at least (a new one 100, where a chip leads with its
    /// time), six weeks of a number line and a new one's chip lines. [S-14101] [S-14500] (owner, 2026-11-20: "the calendar
    /// does not always stretch the contents to the top and bottom although it would be easily possible"): every cell
    /// between, the days' columns and the weeks sharing the card evenly, its chip lines what a week holds.
    /// </summary>
    public static WidgetSizing MonthSizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var grid = ItemGrid.Default();
        if (config.MonthEvents == MonthEvents.Chips)
        {
            double Weeks(int lines) => MonthOver + (MonthWeeks * ItemSize.Optimal(MonthNumberLine + (lines * CalendarFit.ChipLine), grid));
            return WidgetSizing.Range(
                new(7 * MonthTitleColumn, Weeks(MonthPlan.MinLines)),
                new(7 * MonthNaturalColumn, Weeks(MonthPlan.DefaultLines)),
                new(7 * MonthWidestColumn, Weeks(MonthPlan.MaxLines)));
        }

        var week = MonthOver + (MonthWeeks * ItemSize.Optimal(MonthCell(MonthEvents.Dots), grid));

        // [S-15798] Its least is where a day's number is still read (WR-14: "sized down to where its smallest text is still
        // read"): a Caption number line of 16 in the cell's 4 over and under, in a column its two figures fit across.
        var least = new SizeDip(
            grid.Whole((7 * (2 * FamilySpace.S1 + (2 * MonthLayouts.FigureAdvance * FamilyType.Caption.FontSize))) + 1),
            grid.Whole(MonthOver + (MonthWeeks * (FamilyType.Caption.LineHeight + (2 * FamilySpace.S1)))));
        return WidgetSizing.Range(least, new(300, week), new(420, week));
    }

    // ---- [S-11545] [S-11546] week -------------------------------------------------------------------

    /// <summary>[S-11546] A column of the week with titles: at least this wide ([S-2281]).</summary>
    public const double WeekTitleColumn = CalendarFit.WeekTitlesFrom;

    /// <summary>[S-11546] The hour labels' column beside the week's grid: "07:00".</summary>
    public const double HourLabels = 44;

    /// <summary>[S-11545] The most a day's marks take: three marks 4 apart, then a Caption line for its "+n".</summary>
    public static double MarksRoom => CalendarFit.MarksHeight([CalendarFit.WeekMarks + 1]);

    /// <summary>[S-11545] A column's head: its weekday line over its number and the family's 4.</summary>
    public static double WeekHead => FamilyType.Caption.LineHeight + FamilyType.BodyStrong.LineHeight + FamilySpace.S1;

    /// <summary>[S-11545] The day strip: the heads over the most the marks take.</summary>
    public static double Strip => WeekHead + MarksRoom;

    /// <summary>
    /// [S-11545] [S-12815] The day strip's band on <paramref name="grid"/>: its day items from the card's top edge, their heads
    /// at the card's inset over their marks, grown to end on a grid line, the chosen day's rows right under it.
    /// </summary>
    public static double StripBand(ItemGrid grid) => grid.Whole(FamilySpace.Inset + Strip);

    /// <summary>[S-11545] The day strip's height over the chosen day's rows: its band on the default grid (100).</summary>
    public static double WeekRowsOver => StripBand(ItemGrid.Default());

    /// <summary>[S-11546] The week with titles' height over its hours: the heads, the band's one line, the insets.</summary>
    public static double WeekHoursOver => ContentTop + WeekHead + CalendarFit.ChipLine + FamilySpace.Inset;

    /// <summary>[S-14410] A day's column in the day strip: its weekday's letters over its number and its marks.</summary>
    public const double StripColumn = 40;

    /// <summary>
    /// [S-14410] [S-14502] [S-15780] The width a week of <paramref name="days"/> takes: with titles the inset either side, the
    /// hour labels and a column a day; the strip its days' columns alone, each its one size, from the card's left edge.
    /// </summary>
    public static double WeekWidth(int days, bool titles)
        => titles ? (2 * FamilySpace.Inset) + HourLabels + (days * WeekTitleColumn) : days * ItemSize.Optimal(StripColumn, ItemGrid.Default());

    /// <summary>
    /// [S-14410] [S-14502] [S-15780] The days a week <paramref name="width"/> wide shows,
    /// <see cref="CalendarPeriod.WeekMinDays"/>..<see cref="CalendarPeriod.WeekMaxDays"/>: with titles as many 96 columns as
    /// it holds, sharing it; the strip every day the width reaches at its one column (40 in whole cells of
    /// <paramref name="grid"/>, WR-4, WR-6), the last cut by the card's right edge.
    /// </summary>
    public static int WeekDaysIn(double width, bool titles, ItemGrid? grid = null)
    {
        if (!titles)
        {
            return double.IsFinite(width)
                ? ItemSize.Count(width, StripColumn, grid ?? ItemGrid.Default(), CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays)
                : CalendarPeriod.WeekDefaultDays;
        }

        var room = width - (2 * FamilySpace.Inset) - HourLabels;
        var days = double.IsFinite(room) ? (int)Math.Floor((room + LayoutMath.Tolerance) / WeekTitleColumn) : CalendarPeriod.WeekDefaultDays;
        return Math.Clamp(days, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays);
    }

    /// <summary>
    /// [S-11545] [S-11546] [S-14410] [S-14500] The week: the day strip a 40 column a day, or the titles' 96 columns beside
    /// the hours, a new one <see cref="CalendarPeriod.WeekDefaultDays"/> days; the strip's height its chosen day's rows (its
    /// WeekRows a new one's), the titles' its hours (a new one 8); every cell between, the days, rows and hours what the
    /// card holds.
    /// </summary>
    public static WidgetSizing WeekSizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var titles = config.WeekTitles;
        var grid = ItemGrid.Default();
        double Width(int days) => WeekWidth(days, titles);
        if (titles)
        {
            var hour = ItemSize.Optimal(Hour, grid);
            return WidgetSizing.Range(
                new(Width(CalendarPeriod.WeekMinDays), WeekHoursOver + (MinHours * hour)),
                new(Width(CalendarPeriod.WeekDefaultDays), WeekHoursOver + (DefaultHours * hour)),
                new(Width(CalendarPeriod.WeekMaxDays), WeekHoursOver + (MaxHours * hour)));
        }

        var row = ItemSize.Optimal(FamilyShape.Row1, grid);
        return Lined(WidgetSizing.Range(
            new(Width(CalendarPeriod.WeekMinDays), WeekRowsOver + row),
            new(Width(CalendarPeriod.WeekDefaultDays), WeekRowsOver + (ClampWeekRows(config.WeekRows) * row)),
            new(Width(CalendarPeriod.WeekMaxDays), WeekRowsOver + (MaxWeekRows * row))));
    }

    // ---- the kind ---------------------------------------------------------------------------------

    /// <summary>[S-11540]..[S-11546] The sizing of <paramref name="config"/>'s style.</summary>
    public static WidgetSizing Sizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Style switch
        {
            CalendarStyle.Agenda => KitSizing.Sizing(AgendaShown(config), addFooter: false, AgendaWidths),
            CalendarStyle.Year => Lined(YearSizing(config.YearNumbers)),
            CalendarStyle.Day => Lined(DaySizing()),
            CalendarStyle.Week => WeekSizing(config),
            _ => MonthSizing(config),
        };
    }
}

