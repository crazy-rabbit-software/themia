using System.Text.Json;

namespace Themia.Core.Widgets;

/// <summary>
/// One 15-minute step of open-meteo's <c>minutely_15</c> series: the precipitation that falls in the
/// quarter hour ending at <see cref="End"/> (open-meteo stamps a step's sum at its end, [S-5122]).
/// </summary>
public readonly record struct RainStep(DateTimeOffset End, double Millimetres)
{
    public DateTimeOffset Start => End - RainRules.Step;
}

/// <summary>One hour's chance of precipitation, from the hourly block.</summary>
public readonly record struct RainChance(DateTimeOffset Hour, double Percent);

/// <summary>[S-5122] The next couple of hours of precipitation at a place, as the service sent them.</summary>
public sealed record RainNowcast(IReadOnlyList<RainStep> Steps, IReadOnlyList<RainChance> Chances)
{
    /// <summary>The reply this was parsed from, so the offline store can save and re-parse it ([S-5161]).</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>[S-5122] The forecast reply's parser for the rain widget: the trust boundary for it.</summary>
public static class RainWire
{
    /// <summary>
    /// Parse a reply asked with <c>timeformat=unixtime</c>. Not JSON, an error document, or no
    /// <c>minutely_15</c> block with parallel <c>time</c> and <c>precipitation</c> arrays is
    /// rejected. Steps with a bad stamp, a null or an out-of-range amount are dropped one by one,
    /// the rest kept in time order; no usable step at all is <see cref="FetchStatus.Empty"/>.
    /// The hourly chances are an extra: missing or malformed, the steps still stand.
    /// </summary>
    public static WireResult<RainNowcast> Parse(string? json)
    {
        if (OpenMeteoJson.Open(json, out var document, out var problem) is not { } root)
        {
            return WireResult<RainNowcast>.Bad(problem);
        }

        using (document)
        {
            if (!root.TryGetProperty("minutely_15", out var block) || block.ValueKind != JsonValueKind.Object
                || !block.TryGetProperty("time", out var times) || times.ValueKind != JsonValueKind.Array
                || !block.TryGetProperty("precipitation", out var amounts) || amounts.ValueKind != JsonValueKind.Array)
            {
                return WireResult<RainNowcast>.Bad("no minutely_15 precipitation block");
            }

            var steps = Pairs(times, amounts, 0, 500)
                .Select(p => new RainStep(p.At, p.Value))
                .ToList();

            if (steps.Count == 0)
            {
                return WireResult<RainNowcast>.Nothing("no usable 15-minute step in the reply");
            }

            var chances = new List<RainChance>();
            if (root.TryGetProperty("hourly", out var hourly) && hourly.ValueKind == JsonValueKind.Object
                && hourly.TryGetProperty("time", out var hours) && hours.ValueKind == JsonValueKind.Array
                && hourly.TryGetProperty("precipitation_probability", out var percents) && percents.ValueKind == JsonValueKind.Array)
            {
                chances.AddRange(Pairs(hours, percents, 0, 100).Select(p => new RainChance(p.At, p.Value)));
            }

            return WireResult<RainNowcast>.Ok(new RainNowcast(steps, chances) { Source = json! });
        }
    }

    /// <summary>
    /// The usable (time, value) pairs of two parallel arrays, in time order, a repeated stamp once.
    /// Walks the shorter array: "the same length" is the service's promise, not a guarantee.
    /// </summary>
    private static List<(DateTimeOffset At, double Value)> Pairs(JsonElement times, JsonElement values, double min, double max)
    {
        var count = Math.Min(times.GetArrayLength(), values.GetArrayLength());
        var pairs = new SortedDictionary<DateTimeOffset, double>();

        for (var i = 0; i < count; i++)
        {
            if (OpenMeteoJson.Time(times[i]) is { } at && OpenMeteoJson.Number(values[i], min, max) is { } value)
            {
                pairs.TryAdd(at, value);
            }
        }

        return [.. pairs.Select(p => (p.Key, p.Value))];
    }
}

/// <summary>[S-5123] How hard it rains in a quarter hour.</summary>
public enum RainIntensity
{
    None = 0,
    Light = 1,
    Moderate = 2,
    Heavy = 3,
}

/// <summary>[S-5124] The headline: what the next two hours hold, from now.</summary>
public enum RainHeadline
{
    /// <summary>Not enough of the series lies ahead of now to say (a nowcast kept too long offline).</summary>
    Outdated = 0,

    /// <summary>Dry for the whole window.</summary>
    Dry = 1,

    /// <summary>Dry now, rain from <see cref="RainOutlook.Minutes"/> on.</summary>
    StartsIn = 2,

    /// <summary>Raining now, dry from <see cref="RainOutlook.Minutes"/> on.</summary>
    StopsIn = 3,

    /// <summary>Raining now and for the whole window.</summary>
    Continues = 4,
}

/// <summary>[S-5124] The window from now: the headline, its minutes, and the steps for the bars.</summary>
/// <param name="Minutes">For StartsIn / StopsIn: minutes from now, rounded to the step (15, 30, ...); 0 otherwise.</param>
/// <param name="Steps">The steps in the window, the current quarter hour first.</param>
/// <param name="Chance">The current hour's chance of precipitation, when the reply carried one.</param>
/// <param name="Total">The millimetres the window holds.</param>
public sealed record RainOutlook(
    RainHeadline Headline,
    int Minutes,
    IReadOnlyList<RainStep> Steps,
    double? Chance,
    double Total);

/// <summary>[S-5122]..[S-5125] The rules from a nowcast to what the widget says.</summary>
public static class RainRules
{
    public static TimeSpan Step { get; } = TimeSpan.FromMinutes(15);

    /// <summary>[S-5124] The window the widget speaks for: two hours, eight steps.</summary>
    public const int WindowSteps = 8;

    /// <summary>[S-5124] Fewer steps ahead than this (an hour) and the nowcast is too old to speak for now.</summary>
    public const int MinimumSteps = 4;

    /// <summary>
    /// [S-5123] Under this in a quarter hour is a trace, not rain: 0.1 mm is the model's resolution,
    /// and "Rain in 15 min" for a trace that never wets the street is the alarm that teaches people
    /// to ignore the widget.
    /// </summary>
    public const double WetFrom = 0.1;

    /// <summary>
    /// [S-5123] The intensity of a quarter hour, from its hourly rate (four times the step):
    /// light under 2.5 mm/h, moderate under 7.6 mm/h, heavy from there (the meteorological convention).
    /// </summary>
    public static RainIntensity Intensity(double millimetres)
    {
        if (!(millimetres >= WetFrom))
        {
            return RainIntensity.None;
        }

        var perHour = millimetres * 4;
        return perHour < 2.5 ? RainIntensity.Light : perHour < 7.6 ? RainIntensity.Moderate : RainIntensity.Heavy;
    }

    public static bool IsWet(double millimetres) => millimetres >= WetFrom;

    /// <summary>
    /// [S-5124] The outlook at <paramref name="now"/>: the step under way (the one whose quarter hour
    /// holds now) and the ones after it, up to <see cref="WindowSteps"/>. The headline reads the
    /// window: wet now and dry later stops; dry now and wet later starts; the minutes are from now to
    /// the start of the first step that changes it, never below one step (a change under way within
    /// the current quarter is "in 15 min", not "in 0 min").
    /// </summary>
    public static RainOutlook Outlook(RainNowcast nowcast, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(nowcast);

        var window = nowcast.Steps.Where(s => s.End > now).Take(WindowSteps).ToList();
        var chance = Chance(nowcast, now);

        if (window.Count < MinimumSteps)
        {
            return new RainOutlook(RainHeadline.Outdated, 0, window, chance, 0);
        }

        var total = Math.Round(window.Sum(s => s.Millimetres), 1);
        var wetNow = IsWet(window[0].Millimetres);
        var change = window.FindIndex(s => IsWet(s.Millimetres) != wetNow);

        if (change < 0)
        {
            return new RainOutlook(wetNow ? RainHeadline.Continues : RainHeadline.Dry, 0, window, chance, total);
        }

        var minutes = (window[change].Start - now).TotalMinutes;
        var rounded = (int)Math.Max(Step.TotalMinutes, Math.Round(minutes / Step.TotalMinutes) * Step.TotalMinutes);
        return new RainOutlook(wetNow ? RainHeadline.StopsIn : RainHeadline.StartsIn, rounded, window, chance, total);
    }

    /// <summary>The chance for the hour holding now, or null when the reply had none for it.</summary>
    private static double? Chance(RainNowcast nowcast, DateTimeOffset now)
    {
        foreach (var hour in nowcast.Chances)
        {
            if (hour.Hour <= now && now < hour.Hour.AddHours(1))
            {
                return hour.Percent;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-5125] A bar's height as a share of the chart, on a fixed scale (heavy rain fills it) so the
    /// same rain is the same bar on every tile and a drizzle never draws as a downpour. A wet step
    /// is never thinner than a sliver, so it reads as rain.
    /// </summary>
    public static double BarShare(double millimetres)
    {
        if (!IsWet(millimetres))
        {
            return 0;
        }

        const double Full = 7.6 / 4;
        return Math.Clamp(millimetres / Full, 0.08, 1);
    }
}
