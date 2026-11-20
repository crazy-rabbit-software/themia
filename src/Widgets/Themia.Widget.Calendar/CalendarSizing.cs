using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-11540]..[S-11546] The calendar's sizes, each style its own and from the config alone, on the
/// family's tokens (inset 12, the period's line 24 over every style but the agenda, [S-12481], rows 28/40): what a style shows is its settings', never
/// the card's size. Pure.
/// </summary>
public static class CalendarSizing
{
    public const int DefaultAgendaRows = 6;

    /// <summary>[S-569] [S-12481] [S-12834] The period's line (a tool line) and the gap under it, over every style but the agenda: its whole-cell band.</summary>
    public static double NavLine => LineRules.NaturalBand;

    /// <summary>[S-12481] Where a style with the period's line starts its drawing: the card's inset and the line.</summary>
    public static double ContentTop => FamilySpace.ContentTop + NavLine;

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
    /// [S-11542] The year at its own size, its period's line over it, in the inset: its Natural and its Min. The card's
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

    /// <summary>[S-11543] The day's height steps: the inset and the period's line, the band's line and 4, an hour a step, the inset.</summary>
    public static SizeSteps DaySteps(int hours)
        => new(ContentTop + CalendarFit.DayBar + FamilySpace.S1 + FamilySpace.Inset, Hour, MinHours, MaxHours, ClampHours(hours));

    /// <summary>[S-11543] The day: 240..480 wide (a new one 300), its height its hours'.</summary>
    public static WidgetSizing DaySizing(int hours)
        => WidgetSizing.Range(new(240, 1), new(300, 1), new(480, 1)).WithHeightSteps(DaySteps(hours));

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

    /// <summary>[S-11547] The month with titles' height steps: the inset and the period's line, the weekday line, six number lines, a chip line a week a step, the inset.</summary>
    public static SizeSteps MonthLineSteps(int lines)
        => new(ContentTop + FamilyType.Caption.LineHeight + (MonthWeeks * MonthNumberLine) + FamilySpace.Inset,
            MonthWeeks * CalendarFit.ChipLine, MonthPlan.MinLines, MonthPlan.MaxLines, MonthPlan.Lines(lines));

    /// <summary>
    /// [S-11544] The month with dots: 260..420 wide (a new one 300), its weekday letters and six weeks of 28.
    /// [S-11547] With titles: seven columns of 56..180 (a new one 100, where a chip leads with its time), the
    /// height its lines', dragging the bottom edge sets them.
    /// </summary>
    public static WidgetSizing MonthSizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.MonthEvents == MonthEvents.Chips)
        {
            static double Wide(double column) => (2 * FamilySpace.Inset) + (7 * column);
            return WidgetSizing.Range(new(Wide(MonthTitleColumn), 1), new(Wide(MonthNaturalColumn), 1), new(Wide(MonthWidestColumn), 1))
                .WithHeightSteps(MonthLineSteps(config.MonthLines));
        }

        var height = ContentTop + FamilyType.Caption.LineHeight + (MonthWeeks * MonthCell(MonthEvents.Dots)) + FamilySpace.Inset;
        return WidgetSizing.Range(new(260, height), new(300, height), new(420, height));
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

    /// <summary>[S-11545] The day strip's height steps: the strip and 8 over the chosen day's rows (28 each).</summary>
    public static SizeSteps WeekRowSteps(int rows)
        => new(ContentTop + Strip + FamilySpace.S2 + FamilySpace.Inset, FamilyShape.Row1, 1, MaxWeekRows, ClampWeekRows(rows));

    /// <summary>[S-11546] The week with titles' height steps: the heads, the band's one line, an hour a step.</summary>
    public static SizeSteps WeekHourSteps(int hours)
        => new(ContentTop + WeekHead + CalendarFit.ChipLine + FamilySpace.Inset, Hour, MinHours, MaxHours, ClampHours(hours));

    /// <summary>[S-14410] A day's column in the day strip: its weekday's letters over its number and its marks.</summary>
    public const double StripColumn = 40;

    /// <summary>[S-14410] The day strip's width: the inset either side and a 40 column a day.</summary>
    public static SizeSteps StripSteps(int days)
        => new(2 * FamilySpace.Inset, StripColumn, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays, Math.Clamp(days, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays));

    /// <summary>[S-14410] The week with titles' width: the inset either side, the hour labels and a 96 column a day.</summary>
    public static SizeSteps TitleSteps(int days)
        => new((2 * FamilySpace.Inset) + HourLabels, WeekTitleColumn, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays, Math.Clamp(days, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays));

    /// <summary>
    /// [S-11545] [S-11546] [S-14410] The week: the day strip a 40 column a day, or the titles' 96 columns beside the
    /// hours; the width stepped by a day and following the days, which are the card's (a resize sets them).
    /// </summary>
    public static WidgetSizing WeekSizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var days = config.EffectiveWeekDays;
        return config.WeekTitles
            ? WidgetSizing.Range(new(1, 1), new(1, 1), new(1, 1)).WithWidthSteps(TitleSteps(days)).WithHeightSteps(WeekHourSteps(config.WeekHours))
            : WidgetSizing.Range(new(1, 1), new(1, 1), new(1, 1)).WithWidthSteps(StripSteps(days)).WithHeightSteps(WeekRowSteps(config.WeekRows));
    }

    // ---- the kind ---------------------------------------------------------------------------------

    /// <summary>[S-11540]..[S-11546] The sizing of <paramref name="config"/>'s style.</summary>
    public static WidgetSizing Sizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Style switch
        {
            CalendarStyle.Agenda => KitSizing.Sizing(AgendaShown(config), addFooter: false, AgendaWidths),
            CalendarStyle.Year => YearSizing(config.YearNumbers),
            CalendarStyle.Day => DaySizing(config.DayHours),
            CalendarStyle.Week => WeekSizing(config),
            _ => MonthSizing(config),
        };
    }

    /// <summary>[S-2442] The config a card of <paramref name="size"/> means: the rows (or hours) that height holds; a copy when they change.</summary>
    public static WidgetConfig SizeSets(WidgetConfig config, SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config is not CalendarWidgetConfig calendar)
        {
            return config;
        }

        return calendar.Style switch
        {
            CalendarStyle.Agenda => KitSizing.SizeSets(calendar, size, AgendaShown(calendar), addFooter: false),
            CalendarStyle.Day => Counted(calendar, DaySteps(calendar.DayHours).Nearest(size.Height), calendar.DayHours, (c, n) => c.DayHours = n),
            CalendarStyle.Week when calendar.WeekTitles => Week(calendar, TitleSteps(calendar.WeekDays).Nearest(size.Width), WeekHourSteps(calendar.WeekHours).Nearest(size.Height), calendar.WeekHours, (c, n) => c.WeekHours = n),
            CalendarStyle.Week => Week(calendar, StripSteps(calendar.WeekDays).Nearest(size.Width), WeekRowSteps(calendar.WeekRows).Nearest(size.Height), calendar.WeekRows, (c, n) => c.WeekRows = n),
            CalendarStyle.Month when calendar.MonthEvents == MonthEvents.Chips => Counted(calendar, MonthLineSteps(calendar.MonthLines).Nearest(size.Height), calendar.MonthLines, (c, n) => c.MonthLines = n),
            _ => config,
        };
    }

    /// <summary>[S-14410] A week's copy with the days its width holds and the rows or hours its height holds, else the same config.</summary>
    private static WidgetConfig Week(CalendarWidgetConfig config, int days, int wanted, int current, Action<CalendarWidgetConfig, int> set)
    {
        if (days == config.EffectiveWeekDays && wanted == current)
        {
            return config;
        }

        var copy = (CalendarWidgetConfig)config.Clone();
        copy.WeekDays = days;
        set(copy, wanted);
        return copy;
    }

    /// <summary>A copy with <paramref name="wanted"/> set where it differs from <paramref name="current"/>, else the same config.</summary>
    private static WidgetConfig Counted(CalendarWidgetConfig config, int wanted, int current, Action<CalendarWidgetConfig, int> set)
    {
        if (wanted == current)
        {
            return config;
        }

        var copy = (CalendarWidgetConfig)config.Clone();
        set(copy, wanted);
        return copy;
    }
}
