using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.Core.Links;

/// <summary>
/// [S-11910]..[S-11914] The rules of a moment pointed at on a history chart: what a chart's subject is
/// called, where a moment stands across a chart and back, the sample of a series at a moment (the nearest,
/// none outside the span), and the caption a reading shows while it stands there.
/// </summary>
public static class LinkInstants
{
    /// <summary>
    /// [S-11912] A system series' subject: its source, then its target and instance where it has them
    /// (<c>cpu</c>, <c>diskactivity//c:\</c>, <c>ping/example.com</c>, <c>gpu//0</c>), lower-case. A reading and its
    /// history read one series, so they have one subject.
    /// </summary>
    public static string Subject(SeriesInterest series)
    {
        var source = series.Key.Source.ToString().ToLowerInvariant();
        var target = series.Key.Target;
        var instance = series.Instance.ToLowerInvariant();
        return instance.Length > 0 ? $"{source}/{target}/{instance}"
            : target.Length > 0 ? $"{source}/{target}"
            : source;
    }

    /// <summary>[S-11912] A market subject: <c>stock/msft</c>, <c>rate/eur/usd</c>.</summary>
    public static string Subject(string kind, string key) => $"{kind}/{key.Trim().ToLowerInvariant()}";

    /// <summary>
    /// [S-11911] The moment at <paramref name="x"/> on a chart that runs from <paramref name="left"/> (the
    /// span's start) to <paramref name="right"/> (<paramref name="end"/>, the newest), clamped to the span.
    /// </summary>
    public static DateTimeOffset At(double x, double left, double right, TimeSpan span, DateTimeOffset end)
    {
        if (right <= left || span <= TimeSpan.Zero)
        {
            return end;
        }

        var fraction = Math.Clamp((right - x) / (right - left), 0, 1);
        return end - (span * fraction);
    }

    /// <summary>[S-11911] Where <paramref name="at"/> stands on such a chart, or null outside its span.</summary>
    public static double? X(DateTimeOffset at, double left, double right, TimeSpan span, DateTimeOffset end)
    {
        if (right <= left || span <= TimeSpan.Zero)
        {
            return null;
        }

        var ago = end - at;
        if (ago < TimeSpan.Zero || ago > span)
        {
            return null;
        }

        return right - ((right - left) * (ago / span));
    }

    /// <summary>
    /// [S-11913] The sample of an evenly spaced series at <paramref name="at"/>: <paramref name="count"/>
    /// samples <paramref name="period"/> apart, the newest at <paramref name="newest"/>; the nearest one, or
    /// null when the moment is more than half a period outside them.
    /// </summary>
    public static int? Index(int count, TimeSpan period, DateTimeOffset newest, DateTimeOffset at)
    {
        if (count <= 0 || period <= TimeSpan.Zero)
        {
            return null;
        }

        var back = Math.Round((newest - at) / period, MidpointRounding.AwayFromZero);
        return back < 0 || back > count - 1 ? null : count - 1 - (int)back;
    }

    /// <summary>
    /// [S-11913] The sample of a series with its own times (in order) at <paramref name="at"/>: the nearest,
    /// or null for a moment before the first or after the last.
    /// </summary>
    public static int? Nearest(IReadOnlyList<DateTimeOffset> times, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(times);
        if (times.Count == 0 || at < times[0] || at > times[^1])
        {
            return null;
        }

        var best = 0;
        for (var i = 1; i < times.Count; i++)
        {
            if ((times[i] - at).Duration() < (times[best] - at).Duration())
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// [S-11914] The caption of a reading standing at <paramref name="at"/>: by <see cref="InstantCaption.Ago"/>
    /// "32 s ago" within the minute, "4 min ago" within the hour, else the time ("14:05"); a market's moment
    /// with its day ("Thu 12 Nov, 14:05"; "Today, 14:05"); a daily series' the day alone ("Thu 12 Nov").
    /// </summary>
    public static string Caption(DateTimeOffset at, DateTimeOffset now, InstantCaption style = InstantCaption.Ago) => Caption(Strings.Current, at, now, style);

    /// <inheritdoc cref="Caption(DateTimeOffset, DateTimeOffset, InstantCaption)"/>
    public static string Caption(Locale locale, DateTimeOffset at, DateTimeOffset now, InstantCaption style = InstantCaption.Ago)
    {
        var local = at.ToOffset(now.Offset);
        var time = LocaleFormat.Time(locale, TimeOnly.FromDateTime(local.DateTime));
        var heading = Formatting.DayHeading(locale, DateOnly.FromDateTime(local.DateTime), DateOnly.FromDateTime(now.DateTime));
        switch (style)
        {
            case InstantCaption.Day:
                return heading;
            case InstantCaption.DayAndTime:
                return Strings.Format(locale, Str.Links_DayAndTime, ("day", heading), ("time", time));
        }

        var ago = now - at;
        if (ago >= TimeSpan.Zero && ago < TimeSpan.FromMinutes(1))
        {
            return Strings.Format(locale, Str.Links_SecondsAgo, ("count", (int)Math.Round(ago.TotalSeconds)));
        }

        return ago >= TimeSpan.Zero && ago < TimeSpan.FromHours(1)
            ? Strings.Format(locale, Str.Links_MinutesAgo, ("count", (int)ago.TotalMinutes))
            : time;
    }
}

/// <summary>[S-11914] How a reading words the moment it stands at.</summary>
public enum InstantCaption
{
    /// <summary>A system series: "32 s ago", "4 min ago", else the time.</summary>
    Ago = 0,

    /// <summary>A market's bars: the day and the time.</summary>
    DayAndTime = 1,

    /// <summary>A daily series (exchange rates): the day.</summary>
    Day = 2,
}
