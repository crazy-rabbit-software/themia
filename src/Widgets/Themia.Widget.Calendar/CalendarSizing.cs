using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-11540]..[S-11546] The calendar's sizes, each style its own and from the config alone, on the
/// family's tokens (inset 12, the header's 36, rows 28/40): what a style shows is its settings', never
/// the card's size. Pure.
/// </summary>
public static class CalendarSizing
{
    public const int DefaultAgendaRows = 6;

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

    /// <summary>[S-11542] What a year of <paramref name="size"/> draws: always twelve months, 4 × 3; Small squares, Medium the numbers under the weekday letters.</summary>
    public static YearFit Year(ReadoutSize size)
        => size == ReadoutSize.Medium
            ? new YearFit(12, 4, 3, FamilyType.Caption.LineHeight, true, true)
            : new YearFit(12, 4, 3, YearSquare, false, false);

    /// <summary>[S-11542] One size by the Size, with its header: the months' title line and their weeks, in the inset.</summary>
    public static WidgetSizing YearSizing(ReadoutSize size)
    {
        var fit = Year(size);
        var mini = 7 * fit.DayCell;
        var miniHeight = FamilyType.Caption.LineHeight + ((fit.Weekdays ? 7 : 6) * fit.DayCell);
        var width = (2 * FamilySpace.Inset) + (fit.Columns * mini) + ((fit.Columns - 1) * YearGap);
        var height = FamilySpace.ContentTop + (fit.Rows * miniHeight) + ((fit.Rows - 1) * YearGap) + FamilySpace.Inset;
        return WidgetSizing.Fixed(new(width, height));
    }

    // ---- [S-11543] day --------------------------------------------------------------------------------

    /// <summary>[S-11543] An hour of a timeline the config sizes: a row of the family's.</summary>
    public const double Hour = FamilyShape.Row1;

    /// <summary>[S-11543] The day's band: one line, its item's own bar, or chips shared among several; none on a day without.</summary>
    public static DayBandFit DayBand(int items)
        => items <= 0 ? new DayBandFit(0, 0, false) : new DayBandFit(1, CalendarFit.DayBar, items > 1);

    /// <summary>[S-11543] The day's height steps: the header, the band's line and 4, an hour a step, the inset.</summary>
    public static SizeSteps DaySteps(int hours)
        => new(FamilySpace.ContentTop + CalendarFit.DayBar + FamilySpace.S1 + FamilySpace.Inset, Hour, MinHours, MaxHours, ClampHours(hours));

    /// <summary>[S-11543] The day: 240..480 wide (a new one 300), its height its hours'.</summary>
    public static WidgetSizing DaySizing(int hours)
        => WidgetSizing.Range(new(240, 1), new(300, 1), new(480, 1)).WithHeightSteps(DaySteps(hours));

    // ---- [S-11544] month ------------------------------------------------------------------------------

    /// <summary>[S-11544] The weeks a month shows: six, so every month is one height (its own weeks and the next days, dimmed).</summary>
    public const int MonthWeeks = 6;

    /// <summary>[S-11544] The chip lines a chips cell holds under its number.</summary>
    public const int MonthChipLines = 2;

    /// <summary>[S-11544] A month cell's height: its number over two chip lines, or over a dot (a row of the family's).</summary>
    public static double MonthCell(MonthEvents events)
        => events == MonthEvents.Chips
            ? CalendarFit.MonthCellChrome + FamilyType.Caption.LineHeight + (MonthChipLines * CalendarFit.ChipLine)
            : FamilyShape.Row1;

    /// <summary>[S-11544] What a month grid <paramref name="innerWidth"/> wide (the card less its inset) draws: six weeks, the config's look.</summary>
    public static MonthFit Month(MonthEvents events, double innerWidth)
    {
        var column = Math.Max(0, innerWidth) / 7;
        var chips = events == MonthEvents.Chips;
        return new MonthFit(MonthWeeks, CalendarFit.Weekdays(column), FamilyType.Caption.LineHeight, MonthCell(events),
            chips ? MonthDensity.Chips : MonthDensity.Dots, chips ? MonthChipLines : 0, CalendarFit.ChipLine, chips && column >= CalendarFit.ChipTimeFrom, false);
    }

    /// <summary>[S-11544] The month: 260..420 wide (a new one 300), its weekday letters and six weeks of the config's cells.</summary>
    public static WidgetSizing MonthSizing(MonthEvents events)
    {
        var height = FamilySpace.ContentTop + FamilyType.Caption.LineHeight + (MonthWeeks * MonthCell(events)) + FamilySpace.Inset;
        return WidgetSizing.Range(new(260, height), new(300, height), new(420, height));
    }

    // ---- [S-11545] [S-11546] week -------------------------------------------------------------------

    /// <summary>[S-11546] A column of the week with titles: at least this wide ([S-2281]).</summary>
    public const double WeekTitleColumn = CalendarFit.WeekTitlesFrom;

    /// <summary>[S-11546] The hour labels' column beside the week's grid: "07:00".</summary>
    public const double HourLabels = 44;

    /// <summary>[S-11546] The widest a week's column with titles grows.</summary>
    public const double WeekWidestColumn = 200;

    /// <summary>[S-11545] The most a day's marks take: three marks 4 apart, then a Caption line for its "+n".</summary>
    public static double MarksRoom => CalendarFit.MarksHeight([CalendarFit.WeekMarks + 1]);

    /// <summary>[S-11545] A column's head: its weekday line over its number and the family's 4.</summary>
    public static double WeekHead => FamilyType.Caption.LineHeight + FamilyType.BodyStrong.LineHeight + FamilySpace.S1;

    /// <summary>[S-11545] The day strip: the heads over the most the marks take.</summary>
    public static double Strip => WeekHead + MarksRoom;

    /// <summary>[S-11545] The day strip's height steps: the strip and 8 over the chosen day's rows (28 each).</summary>
    public static SizeSteps WeekRowSteps(int rows)
        => new(FamilySpace.ContentTop + Strip + FamilySpace.S2 + FamilySpace.Inset, FamilyShape.Row1, 1, MaxWeekRows, ClampWeekRows(rows));

    /// <summary>[S-11546] The week with titles' height steps: the heads, the band's one line, an hour a step.</summary>
    public static SizeSteps WeekHourSteps(int hours)
        => new(FamilySpace.ContentTop + WeekHead + CalendarFit.ChipLine + FamilySpace.Inset, Hour, MinHours, MaxHours, ClampHours(hours));

    /// <summary>[S-11545] [S-11546] The week: the day strip 240..520 wide, or the titles' columns of at least 96 beside the hours.</summary>
    public static WidgetSizing WeekSizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.WeekTitles)
        {
            return WidgetSizing.Range(new(240, 1), new(300, 1), new(520, 1)).WithHeightSteps(WeekRowSteps(config.WeekRows));
        }

        var days = config.EffectiveWeekDays;
        var least = (2 * FamilySpace.Inset) + HourLabels + (days * WeekTitleColumn);
        var most = (2 * FamilySpace.Inset) + HourLabels + (days * WeekWidestColumn);
        return WidgetSizing.Range(new(least, 1), new(least, 1), new(most, 1)).WithHeightSteps(WeekHourSteps(config.WeekHours));
    }

    // ---- the kind ---------------------------------------------------------------------------------

    /// <summary>[S-11540]..[S-11546] The sizing of <paramref name="config"/>'s style, with its header.</summary>
    public static WidgetSizing Sizing(CalendarWidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Style switch
        {
            CalendarStyle.Agenda => KitSizing.Sizing(AgendaShown(config), addFooter: false, AgendaWidths),
            CalendarStyle.Year => YearSizing(config.YearSize),
            CalendarStyle.Day => DaySizing(config.DayHours),
            CalendarStyle.Week => WeekSizing(config),
            _ => MonthSizing(config.MonthEvents),
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
            CalendarStyle.Week when calendar.WeekTitles => Counted(calendar, WeekHourSteps(calendar.WeekHours).Nearest(size.Height), calendar.WeekHours, (c, n) => c.WeekHours = n),
            CalendarStyle.Week => Counted(calendar, WeekRowSteps(calendar.WeekRows).Nearest(size.Height), calendar.WeekRows, (c, n) => c.WeekRows = n),
            _ => config,
        };
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
