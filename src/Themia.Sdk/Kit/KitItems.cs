using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7002] The bounds every kit item is cut to, so a hostile or broken source can only ever put
/// too much text in a row, never break its layout.
/// </summary>
public static class KitBounds
{
    public const int Id = 256;
    public const int Title = 400;
    public const int Subtitle = 200;
    public const int Preview = 600;
    public const int Badge = 40;
    public const int Badges = 4;
    public const int Heading = 120;
}

/// <summary>
/// [S-7002] The item rules every source's items go through on their way to a list: an item without
/// an id is dropped, ids are unique (the first one wins), strings are cut to <see cref="KitBounds"/>,
/// a link that is not a web address the rules allow is dropped (the row stays, it just opens
/// nothing), and a summary's fraction is clamped to 0..1 or dropped when it is not a number.
/// </summary>
public static class KitItems
{
    private const string Scope = "kit";

    /// <summary>
    /// The items a list may draw, in their order.
    /// </summary>
    /// <param name="webHosts">
    /// The hosts an item may link to (a service's own, https only), or null for any public web
    /// address, http or https (a feed's links point anywhere the feed's author chose).
    /// </param>
    public static IReadOnlyList<KitItem> Clean(IEnumerable<KitItem> items, IReadOnlyCollection<string>? webHosts = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var clean = new List<KitItem>();
        var dropped = 0;

        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > KitBounds.Id || !seen.Add(item.Id))
            {
                dropped++;
                continue;
            }

            clean.Add(item with
            {
                Title = Cut(item.Title, KitBounds.Title) ?? string.Empty,
                Subtitle = Cut(item.Subtitle, KitBounds.Subtitle),
                Preview = Cut(item.Preview, KitBounds.Preview),
                Badges = item.Badges is { Count: > 0 } badges
                    ? [.. badges.Where(b => !string.IsNullOrWhiteSpace(b.Text)).Take(KitBounds.Badges).Select(b => b with { Text = Cut(b.Text, KitBounds.Badge)! })]
                    : null,
                Link = AllowedLink(item.Link, webHosts),
                Group = item.Group is { } group ? group with { Heading = Cut(group.Heading, KitBounds.Heading) } : null,
            });
        }

        if (dropped > 0)
        {
            Log.Debug(Scope, $"dropped {dropped} items without an id or with one seen before");
        }

        return clean;
    }

    /// <summary>[S-7002] A summary with its fraction clamped to 0..1, or without it when it is not a number.</summary>
    public static KitSummary? Clean(KitSummary? summary)
    {
        if (summary is null)
        {
            return null;
        }

        var fraction = summary.Fraction is { } f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : (double?)null;
        return summary with
        {
            Text = Cut(summary.Text, KitBounds.Subtitle) ?? string.Empty,
            Detail = Cut(summary.Detail, KitBounds.Subtitle),
            Fraction = fraction,
        };
    }

    /// <summary>
    /// [S-7002] Whether a link may be opened from a row: an absolute web address with a host, no user
    /// name or password in it, and, when the source names its hosts, https on one of them.
    /// </summary>
    public static Uri? AllowedLink(Uri? link, IReadOnlyCollection<string>? webHosts = null)
    {
        if (link is null || !link.IsAbsoluteUri || string.IsNullOrEmpty(link.Host) || link.UserInfo.Length > 0)
        {
            return null;
        }

        if (webHosts is null)
        {
            return link.Scheme == Uri.UriSchemeHttps || link.Scheme == Uri.UriSchemeHttp ? link : null;
        }

        return link.Scheme == Uri.UriSchemeHttps && webHosts.Contains(link.Host, StringComparer.OrdinalIgnoreCase) ? link : null;
    }

    private static string? Cut(string? text, int max)
    {
        if (text is null)
        {
            return null;
        }

        if (text.Length <= max)
        {
            return text;
        }

        // Never split a surrogate pair: half an emoji is a broken character on screen.
        var end = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..end];
    }
}

/// <summary>[S-7003] How the kit writes numbers and times, through <see cref="LocaleFormat"/> only.</summary>
public static class KitText
{
    /// <summary>
    /// [S-7003] A count as a badge writes it: 999, 1.2k, 12k, 3.4M, in the language's digits, never
    /// rounded up (1,999 is 1.9k, not 2k: a badge does not claim more than there is).
    /// </summary>
    public static string Count(Locale locale, long count)
    {
        if (count < 0)
        {
            return LocaleFormat.Integer(locale, 0);
        }

        if (count < 1000)
        {
            return LocaleFormat.Integer(locale, count);
        }

        return count < 1_000_000 ? Scaled(locale, count, 1000, "k") : Scaled(locale, count, 1_000_000, "M");
    }

    private static string Scaled(Locale locale, long count, long unit, string suffix)
    {
        var whole = count / unit;
        if (whole >= 10)
        {
            return LocaleFormat.Integer(locale, whole) + suffix;
        }

        var tenths = count * 10 / unit;
        return (tenths % 10 == 0 ? LocaleFormat.Integer(locale, whole) : LocaleFormat.Number(locale, tenths / 10.0, 1)) + suffix;
    }

    /// <summary>
    /// [S-1302] An item's age: the shared freshness ("Just now", "5 min. ago") for a week, then the
    /// date ("Mar 4", with the year once it is not this year's).
    /// </summary>
    public static string Age(Locale locale, DateTimeOffset at, DateTimeOffset now)
    {
        if (now - at < TimeSpan.FromDays(7))
        {
            return FetchState.Freshness(locale, at, now);
        }

        var local = at.ToOffset(now.Offset);
        return LocaleFormat.Date(locale, DateOnly.FromDateTime(local.DateTime), local.Year == now.Year ? DateStyle.MonthDayShort : DateStyle.Medium);
    }

    /// <summary>[S-7003] An item's <see cref="KitWhen"/> written as <paramref name="time"/> asks; empty for none.</summary>
    public static string When(Locale locale, KitWhen? when, KitTime time, DateTimeOffset now)
    {
        if (when is not { } w || time == KitTime.None)
        {
            return string.Empty;
        }

        if (w.Day is { } day)
        {
            // A day has no clock time and no age in minutes: it is written as the day it is.
            return LocaleFormat.Date(locale, day, day.Year == DateOnly.FromDateTime(now.DateTime).Year ? DateStyle.MonthDayShort : DateStyle.Medium);
        }

        if (w.At is not { } at)
        {
            return string.Empty;
        }

        return time switch
        {
            KitTime.Age => Age(locale, at, now),
            KitTime.Clock => LocaleFormat.Time(locale, at.ToOffset(now.Offset).DateTime),
            KitTime.Date => LocaleFormat.Date(locale, at.ToOffset(now.Offset).DateTime, DateStyle.MonthDayShort),
            KitTime.Compact => Compact(locale, at, now),
            KitTime.Stamp => Stamp(locale, at, now),
            _ => string.Empty,
        };
    }

    /// <summary>[S-548] "now", "5m", "3h", "2d", then the language's short month-day; a clock ahead of this one reads "now".</summary>
    public static string Compact(Locale locale, DateTimeOffset at, DateTimeOffset now)
    {
        var age = now - at;

        if (age < TimeSpan.FromMinutes(1))
        {
            return LocaleFormat.Relative(locale, TimeSpan.Zero);
        }

        if (age < TimeSpan.FromHours(1))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_MinutesCompact, ("count", (long)age.TotalMinutes));
        }

        if (age < TimeSpan.FromDays(1))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_HoursCompact, ("count", (long)age.TotalHours));
        }

        if (age < TimeSpan.FromDays(7))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_DaysCompact, ("count", (long)age.TotalDays));
        }

        return MonthDay(at.ToOffset(now.Offset), locale);
    }

    /// <summary>[S-548] The clock time within a day, the short weekday within a week, then the short month-day.</summary>
    public static string Stamp(Locale locale, DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToOffset(now.Offset);
        var age = now - at;

        if (age < TimeSpan.FromDays(1))
        {
            return LocaleFormat.Time(locale, local.DateTime);
        }

        return age < TimeSpan.FromDays(7) ? LocaleFormat.WeekdayName(locale, local.DayOfWeek, NameWidth.Short) : MonthDay(local, locale);
    }

    /// <summary>"Sep 28" · "28. Sept.": the language's short month-day.</summary>
    private static string MonthDay(DateTimeOffset at, Locale locale) => LocaleFormat.Date(locale, DateOnly.FromDateTime(at.DateTime), DateStyle.MonthDayShort);
}
