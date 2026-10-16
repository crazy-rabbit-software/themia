using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-5301] How the clock's Date style stands: a column (weekday, day, month) or, on a strip, the day beside the words.</summary>
public enum DateFace
{
    Column = 0,
    Row = 1,
}

/// <summary>
/// [S-5301] [S-5302] What the clock's Date style draws at one size: the day of the month always, as large
/// as the rest leaves it, then the weekday and the month as they fit.
/// </summary>
/// <param name="Face">Column, or row on a wide strip.</param>
/// <param name="Number">The day of the month's step: the hero.</param>
/// <param name="Weekday">Whether the weekday is shown.</param>
/// <param name="WeekdayStep">Its step.</param>
/// <param name="ShortWeekday">"Mon" rather than "Monday": the wide name would not fit.</param>
/// <param name="Month">Whether the month line is shown.</param>
/// <param name="MonthStep">Its step.</param>
/// <param name="ShortMonth">"Oct" rather than "October": the wide name would not fit.</param>
/// <param name="TextWidth">The words' column beside the day in a row; the whole width in a column.</param>
public readonly record struct DatePlan(
    DateFace Face,
    TypeStep Number,
    bool Weekday,
    TypeStep WeekdayStep,
    bool ShortWeekday,
    bool Month,
    TypeStep MonthStep,
    bool ShortMonth,
    double TextWidth);

/// <summary>
/// [S-5301] [S-5302] The clock's Date style ladder, on <see cref="Disclosure"/> like the other styles' ([S-1487]):
/// the day of the month is reserved and takes what the rungs leave ([S-666]: a larger tile sets the
/// date larger, it never adds another fact); the weekday is the first rung, the month the second. Widths are judged on worst-case names in any language, so a German
/// "Donnerstag" or a Portuguese "segunda-feira" never clips.
/// </summary>
public static class DateLayout
{
    /// <summary>The longest wide weekday name: "segunda-feira".</summary>
    public const int WeekdayCharacters = 13;

    /// <summary>The longest short weekday: "Mittw." style abbreviations.</summary>
    public const int ShortWeekdayCharacters = 5;

    /// <summary>The longest wide month name: "październik", "septiembre".</summary>
    public const int MonthCharacters = 11;

    /// <summary>The longest short month: "sept.".</summary>
    public const int ShortMonthCharacters = 5;

    /// <summary>Between the day and its words in a row.</summary>
    public const double Gap = 12;

    /// <summary>"12": the day of the month is two figures at most.</summary>
    private const int NumberCharacters = 2;

    public static DatePlan Plan(WidgetArea box)
    {
        if (!box.IsMeasured)
        {
            return new DatePlan(DateFace.Column, FluentType.Subtitle, false, FluentType.Caption, true, false, FluentType.Caption, true, 0);
        }

        return box.Shape == AreaShape.Wide && box.WidthClass >= SizeClass.Regular
            ? Row(box)
            : Column(box);
    }

    /// <summary>Weekday over the day over the month: every tile but a strip.</summary>
    private static DatePlan Column(WidgetArea box)
    {
        var width = box.Width;
        var height = box.Height;
        var room = new Disclosure(box);
        var floor = FluentType.Subtitle.LineHeight;
        room.Reserve(floor);

        var (weekdayStep, shortWeekday) = WeekdayStep(width, height * 0.2);
        var weekday = room.Take(weekdayStep.LineHeight);

        var (monthStep, shortMonth) = MonthStep(width, height * 0.16);
        var month = room.Take(monthStep.LineHeight);

        var number = FluentType.Hero(width, room.Remaining + floor, NumberCharacters);
        return new DatePlan(DateFace.Column, number, weekday, weekdayStep, shortWeekday, month, monthStep, shortMonth, width);
    }

    /// <summary>A strip: the day as tall as the strip, the weekday over the month beside it.</summary>
    private static DatePlan Row(WidgetArea box)
    {
        var number = FluentType.Hero(box.Width * 0.45, box.Height, NumberCharacters);
        var textWidth = Math.Max(0, box.Width - FluentType.EstimateWidth(number, NumberCharacters) - Gap);
        var room = new Disclosure(textWidth, box.Height);

        var (weekdayStep, shortWeekday) = WeekdayStep(textWidth, box.Height * 0.45);
        var weekday = room.Take(weekdayStep.LineHeight);

        var (monthStep, shortMonth) = MonthStep(textWidth, box.Height * 0.35);
        var month = room.Take(monthStep.LineHeight);

        return new DatePlan(DateFace.Row, number, weekday, weekdayStep, shortWeekday, month, monthStep, shortMonth, textWidth);
    }

    /// <summary>The weekday is the second thing read: up to Title, semibold.</summary>
    private static (TypeStep Step, bool Short) WeekdayStep(double width, double height)
    {
        var isShort = width < FluentType.EstimateWidth(FluentType.Caption, WeekdayCharacters);
        var step = FluentType.Fit(width, height, isShort ? ShortWeekdayCharacters : WeekdayCharacters, FluentType.Caption, FluentType.Title);
        return (FluentType.Strong(step), isShort);
    }

    /// <summary>[S-5302] The month is support: up to Subtitle, regular; a narrow line drops to the short name.</summary>
    private static (TypeStep Step, bool Short) MonthStep(double width, double height)
    {
        var isShort = width < FluentType.EstimateWidth(FluentType.Caption, MonthCharacters);
        var step = FluentType.Fit(width, height, isShort ? ShortMonthCharacters : MonthCharacters, FluentType.Caption, FluentType.Subtitle);
        return (step with { SemiBold = false }, isShort);
    }
}

/// <summary>[S-5302] The clock's Date style words, through <see cref="LocaleFormat"/>: never culture data.</summary>
public static class DateText
{
    public static string Weekday(Locale locale, DateOnly date, bool shortName)
        => LocaleFormat.WeekdayName(locale, date.DayOfWeek, shortName ? NameWidth.Short : NameWidth.Wide);

    /// <summary>The day of the month in the language's own digits.</summary>
    public static string Number(Locale locale, DateOnly date) => LocaleFormat.Integer(locale, date.Day);

    /// <summary>"October", or the short name ("Oct").</summary>
    public static string Month(Locale locale, DateOnly date, bool shortName)
        => LocaleFormat.MonthName(locale, date.Month, shortName ? NameWidth.Short : NameWidth.Wide);

    /// <summary>The whole date for the tooltip: "Monday, October 12, 2026".</summary>
    public static string Full(Locale locale, DateOnly date) => LocaleFormat.Date(locale, date, DateStyle.Full);
}
