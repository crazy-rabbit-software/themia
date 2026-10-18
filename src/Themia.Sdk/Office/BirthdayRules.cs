using System.Globalization;

namespace Themia.Core.Office;

/// <summary>
/// [S-575] Birthdays: when the next one is, what the label says, and how a provider's date shape
/// becomes a <see cref="Birthday"/>. Pure, against an explicit "today".
/// </summary>
public static class BirthdayRules
{
    /// <summary>
    /// Years before this are not birth years. Outlook stores a birthday with no year as year 1604,
    /// and the classic app printed that contact as four hundred years old.
    /// </summary>
    public const int EarliestYear = 1800;

    /// <summary>
    /// The day a birthday falls on in <paramref name="year"/>. 29 February is kept on the 28th in a
    /// year without one — the original only showed it in leap years, so three birthdays in four
    /// never appeared at all. Null for a month or day that cannot be a date.
    /// </summary>
    public static DateOnly? OccurrenceIn(Birthday birthday, int year)
    {
        if (birthday.Month is < 1 or > 12 || birthday.Day < 1 || year is < 1 or > 9999)
        {
            return null;
        }

        var days = DateTime.DaysInMonth(year, birthday.Month);

        if (birthday.Day > days)
        {
            // Only the leap day is moved; a 31 April is not a birthday, it is bad data.
            return birthday.Month == 2 && birthday.Day == 29 ? new DateOnly(year, 2, 28) : null;
        }

        return new DateOnly(year, birthday.Month, birthday.Day);
    }

    /// <summary>Whether the birthday falls on <paramref name="day"/>.</summary>
    public static bool OccursOn(Birthday birthday, DateOnly day) => OccurrenceIn(birthday, day.Year) == day;

    /// <summary>The next time it falls, today included; null when it never can.</summary>
    public static DateOnly? Next(Birthday birthday, DateOnly today)
    {
        var thisYear = OccurrenceIn(birthday, today.Year);
        if (thisYear is { } on && on >= today)
        {
            return on;
        }

        return OccurrenceIn(birthday, today.Year + 1);
    }

    /// <summary>
    /// The age turned on <paramref name="occurrence"/>, or null when the year is unknown or not a
    /// plausible birth year. Worked out from the occurrence, not from today — an agenda that spans
    /// New Year otherwise says the wrong age for every January birthday.
    /// </summary>
    public static int? AgeOn(Birthday birthday, DateOnly occurrence)
        => birthday.Year is { } year && year >= EarliestYear && year <= occurrence.Year
            ? occurrence.Year - year
            : null;

    /// <summary>"Lena Fischer (34)", or the name alone when the age is unknown.</summary>
    public static string Label(Birthday birthday, DateOnly occurrence)
        => AgeOn(birthday, occurrence) is { } age
            ? $"{birthday.Name} ({age.ToString(CultureInfo.InvariantCulture)})"
            : birthday.Name;

    /// <summary>The first word of the name, for a chip too narrow for the whole of it.</summary>
    public static string FirstName(string name)
    {
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? trimmed[..space] : trimmed;
    }

    /// <summary>
    /// A birthday from parts (Google People's <c>{year, month, day}</c>): null without a name or a
    /// valid month and day; a year outside <see cref="EarliestYear"/>..9999 is dropped, not kept.
    /// </summary>
    public static Birthday? From(string? name, int? year, int? month, int? day)
    {
        var who = name?.Trim();
        if (string.IsNullOrEmpty(who) || month is not (>= 1 and <= 12) || day is not (>= 1 and <= 31))
        {
            return null;
        }

        // 29 Feb exists in a leap year; anything past a month's longest length never does.
        if (day > DateTime.DaysInMonth(2000, month.Value))
        {
            return null;
        }

        return new Birthday(who, month.Value, day.Value, year is >= EarliestYear and <= 9999 ? year : null);
    }

    /// <summary>
    /// A birthday from Graph's <c>birthday</c> (<c>1990-05-10T00:00:00Z</c>) or a vCard
    /// <c>BDAY</c> (<c>19900510</c>, <c>1990-05-10</c>, or yearless <c>--0510</c> / <c>--05-10</c>).
    /// </summary>
    public static Birthday? Parse(string? name, string? value)
    {
        var s = value?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }

        var t = s.IndexOf('T', StringComparison.Ordinal);
        if (t > 0)
        {
            s = s[..t];
        }

        int? year = null;
        string monthDay;

        if (s.StartsWith("--", StringComparison.Ordinal))
        {
            monthDay = s[2..].Replace("-", string.Empty, StringComparison.Ordinal);
        }
        else
        {
            var digits = s.Replace("-", string.Empty, StringComparison.Ordinal);
            if (digits.Length != 8 || !int.TryParse(digits[..4], NumberStyles.None, CultureInfo.InvariantCulture, out var y))
            {
                return null;
            }

            year = y;
            monthDay = digits[4..];
        }

        if (monthDay.Length != 4
            || !int.TryParse(monthDay[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(monthDay[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            return null;
        }

        return From(name, year, month, day);
    }

    /// <summary>
    /// The same person in two address books (a work and a personal account) is one birthday, not
    /// two cakes on one day: kept by name and date, first one wins.
    /// </summary>
    public static IReadOnlyList<Birthday> Distinct(IEnumerable<Birthday> birthdays)
    {
        var seen = new HashSet<(string, int, int)>();
        var result = new List<Birthday>();

        foreach (var b in birthdays)
        {
            if (seen.Add((b.Name.ToUpperInvariant(), b.Month, b.Day)))
            {
                result.Add(b);
            }
        }

        return result;
    }
}
