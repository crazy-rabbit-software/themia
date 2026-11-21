using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-11540]..[S-11546] The calendar's sizes, each style its own and from the config alone, on the
/// family's tokens (inset 12, no period's line: that is the calendar header's, [S-14903], rows 28/40): what a style
/// shows is its settings'; how many of the same item (a week's days, a timeline's hours, the chosen day's rows, a day
/// cell's chip lines) is what the card holds ([S-14410], [S-14502]). Every axis takes every grid cell ([S-14500]). Pure.
/// </summary>
public static class CalendarSizing
{
    public const int DefaultAgendaRows = 6;

    /// <summary>
    /// [S-14903] Where every style starts its drawing: the card's inset. The period's line is the calendar header's own
    /// widget ([S-14900]), so no style counts a line over its content.
    /// </summary>
    public static double ContentTop => FamilySpace.ContentTop;

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

    /// <summary>[S-11542] The gap between two small months.</summary>
    public const double YearGap = FamilySpace.S2;

    /// <summary>[S-11542] A small year's day: a square.</summary>
    public const double YearSquare = FamilySpace.S2;

    /// <summary>[S-11542] What a year draws: always twelve months, 4 × 3; squares, or with <paramref name="numbers"/> the numbers under the weekday letters.</summary>
    public static YearFit Year(bool numbers)
        => numbers
            ? new YearFit(12, 4, 3, FamilyType.Caption.LineHeight, true, true)
            : new YearFit(12, 4, 3, YearSquare, false, false);

    /// <summary>[S-11542] The twelve months' block at its own size, in the inset: its width and height.</summary>
    public static SizeDip YearBlock(YearFit fit)
    {
        ArgumentNullException.ThrowIfNull(fit);
        var mini = 7 * fit.DayCell;
        var miniHeight = FamilyType.Caption.LineHeight + ((fit.Weekdays ? 7 : 6) * fit.DayCell);
        return new SizeDip((fit.Columns * mini) + ((fit.Columns - 1) * YearGap), (fit.Rows * miniHeight) + ((fit.Rows - 1) * YearGap));
    }

    /// <summary>
    /// [S-11542] The year at its own size, in the inset: its Natural and its Min. The card's
    /// size is the user's (no Size setting, [S-2449] retired): a larger card draws the months larger, as one
    /// (<see cref="YearScale"/>).
    /// </summary>
    public static WidgetSizing YearSizing(bool numbers)
    {
        var block = YearBlock(Year(numbers));
        var box = new SizeDip(block.Width + (2 * FamilySpace.Inset), ContentTop + block.Height + FamilySpace.Inset);
        return WidgetSizing.Fixed(box);
    }

    /// <summary>[S-11542] How much larger the months are drawn in a room of <paramref name="width"/> × <paramref name="height"/> (the inset's inside): never under 1.</summary>
    public static double YearScale(YearFit fit, double width, double height)
    {
        var block = YearBlock(fit);
        var scale = Math.Min(width / block.Width, height / block.Height);
        return double.IsFinite(scale) ? Math.Max(1, scale) : 1;
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
    public static int HoursIn(double available, ItemGrid grid) => ItemSize.Count(available, Hour, grid, MinHours, HoursInDay);

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
    /// [S-15671] The day's band row over its hours: <paramref name="lift"/> (the card's inset where the row stands on the
    /// card's top edge, none under a state's row), its line and 4 under it, the line's inset its own padding; none without
    /// an all-day item.
    /// </summary>
    public static double DayBandRow(DayBandFit band, double lift)
    {
        ArgumentNullException.ThrowIfNull(band);
        return band.Lines > 0 ? Math.Max(0, lift) + (band.Lines * band.LineHeight) + FamilySpace.S1 : 0;
    }

    /// <summary>[S-11543] [S-15671] The day's height over its hours: its band row on the card's top edge (the hours run to the foot).</summary>
    public static double DayOver => DayBandRow(DayBand(1), ContentTop);

    /// <summary>[S-11543] [S-14500] The day: 240..480 wide (a new one 300), its height <see cref="DefaultHours"/> hours, 4..16 at least and most.</summary>
    public static WidgetSizing DaySizing()
    {
        var hour = ItemSize.Optimal(Hour, ItemGrid.Default());
        return WidgetSizing.Range(new(240, DayOver + (MinHours * hour)), new(300, DayOver + (DefaultHours * hour)), new(480, DayOver + (MaxHours * hour)));
    }

    // ---- [S-11544] month ------------------------------------------------------------------------------

    /// <summary>[S-11544] The weeks a month shows: six, so every month is one height (its own weeks and the next days, dimmed).</summary>
    public const int MonthWeeks = 6;

    /// <summary>[S-11547] A day cell with titles: its padding and its number's line, over which the chip lines go.</summary>
    public static double MonthNumberLine => CalendarFit.MonthCellChrome + FamilyType.Caption.LineHeight;

    /// <summary>[S-11547] A column of the month with titles: at least this wide (a title's first letters).</summary>
    public const double MonthTitleColumn = 56;

    /// <summary>[S-11547] A new month with titles' column: wide enough for a chip's start time (<see cref="CalendarFit.ChipTimeFrom"/>).</summary>
    public const double MonthNaturalColumn = CalendarFit.ChipTimeFrom;

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
    /// as the month lays it (<see cref="MonthLayouts.Plan"/>): the card's inset and the weekday line under it ([S-14903]: no
    /// period's line).
    /// </summary>
    public static double MonthOver => ItemSize.Optimal(ContentTop + FamilyType.Caption.LineHeight, ItemGrid.Default());

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
        return WidgetSizing.Range(new(260, week), new(300, week), new(420, week));
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

    /// <summary>[S-14410] [S-14502] The width a week of <paramref name="days"/> takes: the inset either side, the hour labels with titles, a column a day.</summary>
    public static double WeekWidth(int days, bool titles)
        => (2 * FamilySpace.Inset) + (titles ? HourLabels + (days * WeekTitleColumn) : days * StripColumn);

    /// <summary>
    /// [S-14410] [S-14502] The days a week <paramref name="width"/> wide shows: as many columns (40 in the strip, 96 with
    /// titles) as it holds, <see cref="CalendarPeriod.WeekMinDays"/>..<see cref="CalendarPeriod.WeekMaxDays"/>, sharing it.
    /// </summary>
    public static int WeekDaysIn(double width, bool titles)
    {
        var room = width - (2 * FamilySpace.Inset) - (titles ? HourLabels : 0);
        var column = titles ? WeekTitleColumn : StripColumn;
        var days = double.IsFinite(room) ? (int)Math.Floor((room + LayoutMath.Tolerance) / column) : CalendarPeriod.WeekDefaultDays;
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
        return WidgetSizing.Range(
            new(Width(CalendarPeriod.WeekMinDays), WeekRowsOver + row),
            new(Width(CalendarPeriod.WeekDefaultDays), WeekRowsOver + (ClampWeekRows(config.WeekRows) * row)),
            new(Width(CalendarPeriod.WeekMaxDays), WeekRowsOver + (MaxWeekRows * row)));
    }

    // ---- [S-14901] the calendar header -------------------------------------------------------------

    /// <summary>[S-14901] The least header: the longest period's title ("September 2026", "Sep 28 – Oct 4") beside its two arrows.</summary>
    public const double HeaderNarrowest = 200;

    /// <summary>[S-14901] A new header: as wide as a new month under it.</summary>
    public const double HeaderNatural = 300;

    /// <summary>
    /// [S-14901] A header's one height: the card's inset over its tool line, the line and the gap under it, whole grid
    /// cells (<see cref="LineRules.BandOver"/>, 40 on the default grid): one line at every width, nothing over or under it.
    /// </summary>
    public static double HeaderHeight => ItemSize.Optimal(LineRules.BandOver, ItemGrid.Default());

    /// <summary>[S-14901] The header: any width from its title and arrows to a widest month with titles, one line tall.</summary>
    public static WidgetSizing HeaderSizing()
        => WidgetSizing.Range(new(HeaderNarrowest, HeaderHeight), new(HeaderNatural, HeaderHeight), new(7 * MonthWidestColumn, HeaderHeight));

    // ---- the kind ---------------------------------------------------------------------------------

    /// <summary>[S-11540]..[S-11546] The sizing of <paramref name="config"/>'s style.</summary>
    public static WidgetSizing Sizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Style switch
        {
            CalendarStyle.Agenda => KitSizing.Sizing(AgendaShown(config), addFooter: false, AgendaWidths),
            CalendarStyle.Year => YearSizing(config.YearNumbers),
            CalendarStyle.Day => DaySizing(),
            CalendarStyle.Week => WeekSizing(config),
            _ => MonthSizing(config),
        };
    }
}
