using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-3738] A time of day or a date typed on the properties menu ([S-3721]), read the same way by
/// every widget that asks for one (an alarm's time, a countdown's target). Strict on purpose: what is
/// typed is the trust boundary, so a form that could mean two things is refused rather than guessed
/// at, and a refused text leaves the setting as it was.
/// </summary>
public static class TypedTime
{
    /// <summary>
    /// A time of day as minutes after midnight, or null: "7:30", "07:30", "19:30", "7.30", a bare hour
    /// ("7", "19"), each with an optional "am"/"pm" ("7:30 pm", "7pm"), where the hour is then 1 to 12.
    /// </summary>
    public static int? TimeOfDay(string? typed)
    {
        var text = (typed ?? string.Empty).Trim().ToLowerInvariant()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(".", ":", StringComparison.Ordinal);

        bool? pm = null;
        if (text.EndsWith("am", StringComparison.Ordinal) || text.EndsWith("pm", StringComparison.Ordinal))
        {
            pm = text.EndsWith("pm", StringComparison.Ordinal);
            text = text[..^2];
        }

        var parts = text.Split(':');
        if (parts.Length > 2
            || parts[0].Length is 0 or > 2
            || (parts.Length == 2 && parts[1].Length != 2)
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours))
        {
            return null;
        }

        var minutes = 0;
        if ((parts.Length == 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes)) || minutes > 59)
        {
            return null;
        }

        if (pm is { } afternoon)
        {
            if (hours is < 1 or > 12)
            {
                return null;
            }

            hours = (hours % 12) + (afternoon ? 12 : 0);
        }
        else if (hours > 23)
        {
            return null;
        }

        return (hours * 60) + minutes;
    }

    /// <summary>
    /// A calendar day, or null: the year first ("2026-12-24", "2026/12/24") or the day first with
    /// dots ("24.12.2026", "24.12.26" for 2026). A slash with the day or the month first
    /// ("12/11/2026") means two days in two countries, so it is refused; so is a day the month does not have.
    /// </summary>
    public static DateOnly? Date(string? typed)
    {
        var text = (typed ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        int year, month, day;

        var isoParts = text.Split('-', '/');
        var dotParts = text.Split('.');
        if (isoParts.Length == 3 && isoParts[0].Length == 4)
        {
            if (!Number(isoParts[0], 4, out year) || !Number(isoParts[1], 2, out month) || !Number(isoParts[2], 2, out day))
            {
                return null;
            }
        }
        else if (dotParts.Length == 3)
        {
            if (!Number(dotParts[0], 2, out day) || !Number(dotParts[1], 2, out month) || !Year(dotParts[2], out year))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        return new DateOnly(year, month, day);
    }

    /// <summary>"2026-12-24": a day as <see cref="Date"/> reads it back, whatever the language.</summary>
    public static string DateText(DateOnly date)
        => string.Create(CultureInfo.InvariantCulture, $"{date.Year:0000}-{date.Month:00}-{date.Day:00}");

    private static bool Number(string text, int most, out int value)
    {
        value = 0;
        return text.Length > 0 && text.Length <= most
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Four figures, or two for this century ("26" is 2026).</summary>
    private static bool Year(string text, out int year)
    {
        year = 0;
        if (text.Length is not (2 or 4) || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            return false;
        }

        if (text.Length == 2)
        {
            year += 2000;
        }

        return true;
    }
}
