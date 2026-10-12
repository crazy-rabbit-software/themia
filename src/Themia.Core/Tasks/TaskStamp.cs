using System.Globalization;
using System.Text.RegularExpressions;

namespace Themia.Core.Tasks;

/// <summary>
/// [S-593] [S-612] A task's date as a provider writes it, parsed once at the trust boundary: either a
/// calendar <b>day</b> (midnight of the intended day under any encoding, or a bare date) or a real
/// <b>instant</b> (any other time; zoneless means UTC, as Graph's own zoneless times are).
/// <para>
/// Which <i>day</i> an instant falls on depends on the viewer's zone, so that is answered at render
/// time by <see cref="DayIn"/>, never frozen at fetch time. A day is the day for every viewer:
/// converting it through UTC would push everyone west of Greenwich back a day.
/// </para>
/// </summary>
public readonly partial record struct TaskStamp
{
    private TaskStamp(DateOnly? day, DateTimeOffset? instant)
    {
        Day = day;
        Instant = instant;
    }

    /// <summary>The calendar day meant, when the value was written as a day.</summary>
    public DateOnly? Day { get; }

    /// <summary>The moment meant, in UTC, when the value was written as a time of day.</summary>
    public DateTimeOffset? Instant { get; }

    public static TaskStamp OfDay(DateOnly day) => new(day, null);

    public static TaskStamp At(DateTimeOffset instant) => new(null, instant.ToUniversalTime());

    /// <summary>The day this stamp falls on for a viewer in <paramref name="viewer"/>.</summary>
    public DateOnly DayIn(TimeZoneInfo viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        return Day ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Instant!.Value, viewer).DateTime);
    }

    /// <summary>
    /// A provider's or a stored value, or null when it is absent or not a date. Never throws: this
    /// is the boundary, and a value that cannot be read is no value.
    /// </summary>
    public static TaskStamp? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = StampPattern().Match(raw);
        if (!match.Success || !TryDate(match, out var written))
        {
            return null;
        }

        var hasTime = match.Groups[4].Success;
        var midnight = !hasTime
            || (match.Groups[4].Value == "00" && match.Groups[5].Value == "00"
                && (!match.Groups[6].Success || match.Groups[6].Value == "00"));

        if (midnight)
        {
            // Date-only under every encoding: the written date is the day meant.
            return OfDay(written);
        }

        // A real instant. Zoneless means UTC, as the calendar reads Graph's own zoneless times.
        return DateTimeOffset.TryParse(
                raw.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var instant)
            ? At(instant)
            : null;
    }

    /// <summary>
    /// The form the offline store keeps: <c>yyyy-MM-dd</c> for a day, ISO 8601 UTC for an instant.
    /// <see cref="Parse"/> reads either back to an equal stamp.
    /// </summary>
    public string Wire() => Day is { } day
        ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : Instant!.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static bool TryDate(Match match, out DateOnly day)
    {
        day = default;
        var year = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        var dayOfMonth = int.Parse(match.Groups[3].ValueSpan, CultureInfo.InvariantCulture);

        if (year < 1 || month is < 1 or > 12 || dayOfMonth < 1 || dayOfMonth > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        day = new DateOnly(year, month, dayOfMonth);
        return true;
    }

    /// <summary><c>YYYY-MM-DD</c>, optionally followed by a time; the wall clock exactly as written.</summary>
    [GeneratedRegex(@"^\s*(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?)?", RegexOptions.CultureInvariant)]
    private static partial Regex StampPattern();
}
