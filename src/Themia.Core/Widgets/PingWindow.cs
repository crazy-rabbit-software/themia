using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-2428] What a window of probes says.</summary>
/// <param name="Sent">Probes in the window.</param>
/// <param name="Answered">Of those, answered.</param>
/// <param name="Last">The newest probe's latency; null when it went unanswered (or there is none).</param>
/// <param name="Min">The fastest answer in the window.</param>
/// <param name="Average">The mean of the answers.</param>
/// <param name="Max">The slowest answer.</param>
/// <param name="Jitter">The mean difference between consecutive answers; null with fewer than two.</param>
/// <param name="Loss">Unanswered share of the window, 0..1; null with no probe.</param>
public readonly record struct PingWindowStats(int Sent, int Answered, double? Last, double? Min, double? Average, double? Max, double? Jitter, double? Loss);

/// <summary>
/// [S-2427] [S-2428] The latest probes of one host, oldest first, as many as the window holds, and
/// the statistics over them. One per ping details widget; touched only on the UI thread.
/// </summary>
public sealed class PingWindow
{
    private readonly Queue<double?> _samples = new();
    private int _capacity;

    public PingWindow(int capacity) => _capacity = Math.Max(1, capacity);

    /// <summary>How many probes the statistics cover. Shrinking drops the oldest.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Max(1, value);
            Trim();
        }
    }

    public int Count => _samples.Count;

    /// <summary>Record one probe: a latency in ms, or null for no answer. A nonsense latency is no answer.</summary>
    public void Add(double? latency)
    {
        _samples.Enqueue(latency is { } ms && double.IsFinite(ms) && ms >= 0 ? ms : null);
        Trim();
    }

    public void Clear() => _samples.Clear();

    /// <summary>
    /// [S-2428] The statistics. Jitter is the mean absolute difference between answers that follow
    /// one another in the window (RFC 3550's idea, without its smoothing, so the figure is exactly
    /// the window's): a lost probe between two answers does not break the pair, and fewer than two
    /// answers have no jitter. Loss counts every probe, answered or not.
    /// </summary>
    public PingWindowStats Stats()
    {
        var sent = _samples.Count;

        if (sent == 0)
        {
            return new PingWindowStats(0, 0, null, null, null, null, null, null);
        }

        var answered = 0;
        double sum = 0, min = double.MaxValue, max = double.MinValue, swing = 0;
        double? previous = null, last = null;
        var pairs = 0;

        foreach (var sample in _samples)
        {
            last = sample;

            if (sample is not { } ms)
            {
                continue;
            }

            answered++;
            sum += ms;
            min = Math.Min(min, ms);
            max = Math.Max(max, ms);

            if (previous is { } before)
            {
                swing += Math.Abs(ms - before);
                pairs++;
            }

            previous = ms;
        }

        var loss = (double)(sent - answered) / sent;

        return answered == 0
            ? new PingWindowStats(sent, 0, null, null, null, null, null, loss)
            : new PingWindowStats(sent, answered, last, min, sum / answered, max, pairs > 0 ? swing / pairs : null, loss);
    }

    private void Trim()
    {
        while (_samples.Count > _capacity)
        {
            _samples.Dequeue();
        }
    }
}

/// <summary>[S-2426]..[S-2429] The ping statistics widget's sheet.</summary>
public static class PingDetailRules
{
    /// <summary>"999 ms".</summary>
    public const double HeroCharacters = 6;

    /// <summary>
    /// [S-2429] The sheet: the latest latency as the hero (dimmed "--" while checking or after a lost
    /// probe) with the status (Online, Offline, Checking…) under it — the header already names the host; then average, minimum, maximum, jitter, packet loss and how many
    /// probes were answered. Before any probe is back only the host is said.
    /// </summary>
    /// <param name="status">What the last probe said ([S-1170]), for the hero's tooltip.</param>
    public static DetailSheet Sheet(Locale locale, PingWindowStats stats, string host, PingStatus status)
    {
        ArgumentNullException.ThrowIfNull(host);
        var facts = new List<DetailFact>(6);

        if (stats.Sent > 0)
        {
            string Ms(double? value) => value is { } v ? Formatting.Latency(locale, v) : "--";

            facts.Add(new(Strings.Get(locale, Str.Details_Ping_Average), Ms(stats.Average), stats.Average is not null));
            facts.Add(new(Strings.Get(locale, Str.Details_Ping_Minimum), Ms(stats.Min), stats.Min is not null));
            facts.Add(new(Strings.Get(locale, Str.Details_Ping_Maximum), Ms(stats.Max), stats.Max is not null));
            facts.Add(new(Strings.Get(locale, Str.Details_Ping_Jitter), Ms(stats.Jitter), stats.Jitter is not null));
            facts.Add(new(Strings.Get(locale, Str.Details_Ping_Loss), Formatting.Percent(locale, stats.Loss ?? 0)));
            facts.Add(new(Strings.Get(locale, Str.Details_Ping_Answered), Strings.Format(locale, Str.Details_Ping_AnsweredOf,
                ("answered", LocaleFormat.Integer(locale, stats.Answered)), ("sent", LocaleFormat.Integer(locale, stats.Sent)))));
        }

        return stats.Last is { } last
            ? new DetailSheet(Formatting.Latency(locale, last), PingStats.StatusText(locale, status), facts, [])
            : new DetailSheet("--", PingStats.StatusText(locale, status), facts, [], Note: $"{host} · {PingStats.StatusText(locale, status)}", HeroAvailable: false);
    }
}
