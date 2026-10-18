using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>
/// One day on the wall calendar, with no time and no zone attached.
/// <para>
/// Three widgets in this app answer questions about days — was this habit done today, what is the
/// streak, which square of the heatmap is this — and every one of them is a place where doing the
/// arithmetic on <i>instants</i> goes wrong. Subtracting two timestamps and dividing by 24 hours is
/// off by one on the two days a year that are 23 and 25 hours long; comparing UTC dates moves the
/// boundary to 1am or 2am local; reading the clock twice in one render can straddle midnight and
/// draw two different ideas of "today" in the same frame.
/// </para>
/// <para>
/// So a day is a <b>calendar</b> value here, and all arithmetic is calendar arithmetic. A DST day is
/// still one day long because "one day later" means "the next square on the calendar", not "plus
/// 86400 seconds". The only place a clock is read is <see cref="From"/>, which every caller passes an
/// explicit instant to — nothing in this file can reach the system clock, which is what makes the
/// whole of it testable at any date and in any zone.
/// </para>
/// </summary>
/// <param name="Date">The calendar date itself.</param>
public readonly record struct LocalDay(DateOnly Date) : IComparable<LocalDay>
{
    /// <summary>How many characters <see cref="Key"/> always is: <c>YYYY-MM-DD</c>.</summary>
    private const int KeyLength = 10;

    /// <summary>
    /// The day <paramref name="moment"/> fell on, in <paramref name="moment"/>'s own offset.
    /// <para>
    /// The offset the value carries is the answer, not <see cref="TimeZoneInfo.Local"/>: a mood
    /// logged at 11pm in Zurich belongs to that day for good, and must not slide onto the previous
    /// one because the laptop is now in Chicago. Callers pass <c>DateTimeOffset.Now</c> for "today"
    /// and the stored stamp for a record, and both read the same way.
    /// </para>
    /// </summary>
    public static LocalDay From(DateTimeOffset moment) => new(DateOnly.FromDateTime(moment.DateTime));

    public int Year => Date.Year;

    public int Month => Date.Month;

    public int Day => Date.Day;

    public DayOfWeek DayOfWeek => Date.DayOfWeek;

    /// <summary>
    /// The persisted form, <c>YYYY-MM-DD</c>, written invariant.
    /// <para>
    /// Spelled out rather than delegated to a format string so it cannot pick up a culture. The app
    /// builds with <c>InvariantGlobalization</c>, so today there is no culture to pick up — but this
    /// string is in the user's state file forever, and a storage format that depends on a build
    /// switch is a migration waiting to happen.
    /// </para>
    /// </summary>
    public string Key =>
        string.Concat(
            Year.ToString("D4", CultureInfo.InvariantCulture),
            "-",
            Month.ToString("D2", CultureInfo.InvariantCulture),
            "-",
            Day.ToString("D2", CultureInfo.InvariantCulture));

    /// <summary>
    /// Read a persisted key. Strict on purpose: the state file is hand-editable, so anything that is
    /// not exactly <c>YYYY-MM-DD</c> is refused rather than guessed at. A refused key is dropped by
    /// the caller, never turned into a nearby day that would move a streak.
    /// </summary>
    public static bool TryParse(string? key, out LocalDay day)
    {
        day = default;

        if (key is null || key.Length != KeyLength || key[4] != '-' || key[7] != '-')
        {
            return false;
        }

        if (!TryDigits(key, 0, 4, out var year)
            || !TryDigits(key, 5, 2, out var month)
            || !TryDigits(key, 8, 2, out var dayOfMonth))
        {
            return false;
        }

        // The calendar itself is the validator: this is what refuses 2025-02-29 while accepting
        // 2024-02-29, which no amount of range checking on the fields would get right.
        if (year < 1 || month is < 1 or > 12 || dayOfMonth < 1 || dayOfMonth > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        day = new LocalDay(new DateOnly(year, month, dayOfMonth));
        return true;
    }

    private static bool TryDigits(string text, int start, int length, out int value)
    {
        value = 0;

        for (var i = start; i < start + length; i++)
        {
            var c = text[i];

            // Not char.IsDigit: that accepts the digits of every other script, and "٢٠٢٥-٠١-٠١"
            // is not a key this app ever wrote.
            if (c is < '0' or > '9')
            {
                return false;
            }

            value = (value * 10) + (c - '0');
        }

        return true;
    }

    /// <summary>The day <paramref name="days"/> squares along the calendar. Clamped at the ends of <see cref="DateOnly"/>.</summary>
    public LocalDay AddDays(int days)
    {
        var number = (long)Date.DayNumber + days;
        var clamped = (int)Math.Clamp(number, DateOnly.MinValue.DayNumber, DateOnly.MaxValue.DayNumber);
        return new LocalDay(DateOnly.FromDayNumber(clamped));
    }

    /// <summary>Whole calendar days from this day to <paramref name="other"/>. Negative when it is behind.</summary>
    public int DaysUntil(LocalDay other) => other.Date.DayNumber - Date.DayNumber;

    /// <summary>
    /// The first day of the week this day falls in, for a chosen week start.
    /// <para>
    /// The week start is chosen rather than read: <c>InvariantGlobalization</c> leaves no culture
    /// to ask, and the invariant culture's answer would be Sunday for everyone, which is wrong for
    /// most of the world. So it is a setting, and this takes it as an argument.
    /// </para>
    /// </summary>
    public LocalDay StartOfWeek(DayOfWeek weekStart)
    {
        var offset = ((int)DayOfWeek - (int)weekStart + 7) % 7;
        return AddDays(-offset);
    }

    public int CompareTo(LocalDay other) => Date.CompareTo(other.Date);

    public static bool operator <(LocalDay left, LocalDay right) => left.CompareTo(right) < 0;

    public static bool operator >(LocalDay left, LocalDay right) => left.CompareTo(right) > 0;

    public static bool operator <=(LocalDay left, LocalDay right) => left.CompareTo(right) <= 0;

    public static bool operator >=(LocalDay left, LocalDay right) => left.CompareTo(right) >= 0;

    public override string ToString() => Key;
}
