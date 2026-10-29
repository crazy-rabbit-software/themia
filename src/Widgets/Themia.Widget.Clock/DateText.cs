using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-6744] How the clock's Date style stands: a column (weekday, day, month) or, on a strip, the day beside the words.</summary>
public enum DateFace
{
    Column = 0,
    Row = 1,
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
