using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Metrics;

/// <summary>
/// [S-16310] One reading of a metric: its value, a pair's second (the high, the writes, the upload, the gusts), a candle's
/// open, high, low and close (its value the close).
/// </summary>
public readonly record struct MetricValue(double Value, double? Second = null, Candle? Candle = null)
{
    /// <summary>Its lowest figure: what the graph's range must hold.</summary>
    public double Low => Candle?.Low ?? (Second is { } s ? Math.Min(Value, s) : Value);

    /// <summary>Its highest figure.</summary>
    public double High => Candle?.High ?? (Second is { } s ? Math.Max(Value, s) : Value);
}

/// <summary>
/// [S-16310] A metric's series as the graph draws it: a slot a step from <see cref="Start"/> (oldest first; a slot with no
/// reading null, a gap), the reading now, and the slot now stands in. Each slot is <see cref="Step"/> long, or stands at its
/// own time (<see cref="Times"/>: a quote's bars, the ECB's working days).
/// </summary>
public sealed record MetricSeries(MetricShape Shape, IReadOnlyList<MetricValue?> Slots, DateTimeOffset Start, TimeSpan Step)
{
    /// <summary>The reading now: the newest of the hub, the hour or day under way, the last price; null without one.</summary>
    public MetricValue? Now { get; init; }

    /// <summary>[S-16310] The slots' own times where they are not a step apart; null where they are.</summary>
    public IReadOnlyList<DateTimeOffset>? Times { get; init; }

    /// <summary>
    /// [S-16901] Slot 0's absolute place in time, counted in <see cref="Step"/>s (the hub's points ever added, whole local hours,
    /// day numbers): a fold's runs are fixed by it, so a run's members never change as the series moves on. Unused where
    /// <see cref="Times"/> holds (a run there is fixed by its members' own times).
    /// </summary>
    public long Origin { get; init; }

    /// <summary>
    /// [S-16901] The slots just before the span, oldest first (the last one stands right before slot 0): never drawn, there only so
    /// the oldest run a fold draws holds every member of its slot, and keeps its value as the span moves on.
    /// </summary>
    public IReadOnlyList<MetricValue?> Before { get; init; } = [];

    /// <summary>The time slot <paramref name="slot"/> stands for (its start).</summary>
    public DateTimeOffset At(int slot)
        => Times is { Count: > 0 } times ? times[Math.Clamp(slot, 0, times.Count - 1)] : Start + (Step * slot);

    /// <summary>Whether any slot holds a reading.</summary>
    public bool Any => Slots.Any(s => s is not null);

    /// <summary>[S-16311] A series of <paramref name="count"/> empty slots (no reading yet): the graph still stands at its foot (WR-10).</summary>
    public static MetricSeries Empty(MetricShape shape, int count, DateTimeOffset start, TimeSpan step)
        => new(shape, new MetricValue?[Math.Max(2, count)], start, step);
}

/// <summary>
/// [S-16311] The adapters: each source's data as a metric's series, pure. The hub's points a period apart up to now; a
/// place's hours and days on whole hours and days from the one under way (a forecast) or up to it (history); a quote's
/// bars and the ECB's days at their own times. A slot the data has no figure for is a gap, never a 0.
/// </summary>
public static class MetricAdapters
{
    /// <summary>[S-16311] How many slots a span of the hub's holds at its period: two at least.</summary>
    public static int HubSlots(MetricSpan span, int periodSeconds)
        => Math.Max(2, span.Count / Math.Max(1, periodSeconds));

    /// <summary>
    /// [S-16311] The hub's series: the newest points that fit the span, the newest at the right end (a history still filling
    /// grows in from the right); a share stays a fraction (0..1), anything else its value; a missed probe or a point without
    /// a figure a gap. The reading now is the hub's own (null while it has none or it is unavailable). [S-16901] The points
    /// the history holds before the span are its <see cref="MetricSeries.Before"/>, and slot 0 stands at the newest point's
    /// absolute position (<paramref name="added"/>, the points ever added; the history's length by default) less the span.
    /// </summary>
    public static MetricSeries Hub(MetricDef metric, IReadOnlyList<SeriesPoint> history, MetricValue? now, MetricSpan span, int periodSeconds, DateTimeOffset at,
        long? added = null)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(history);
        var feed = metric.Feed(MetricStep.Live);
        var count = HubSlots(span, periodSeconds);
        var slots = new MetricValue?[count];
        var taken = Math.Min(count, history.Count);
        MetricValue? Value(SeriesPoint point) => point.Missed || !double.IsFinite(point.First)
            ? null
            : new MetricValue(point.First, feed.Shape == MetricShape.Pair && double.IsFinite(point.Second) ? point.Second : null);
        for (var i = 0; i < taken; i++)
        {
            slots[count - taken + i] = Value(history[history.Count - taken + i]);
        }

        var before = new MetricValue?[history.Count - taken];
        for (var i = 0; i < before.Length; i++)
        {
            before[i] = Value(history[i]);
        }

        var period = TimeSpan.FromSeconds(Math.Max(1, periodSeconds));
        return new MetricSeries(feed.Shape, slots, at - (period * (count - 1)), period)
        {
            Now = now,
            Origin = (added ?? history.Count) - count,
            Before = before,
        };
    }

    /// <summary>[S-16311] The window of a span from the step under way: forward from it (a forecast), or back to it (history).</summary>
    public static DateTimeOffset WindowStart(MetricSpan span, DateTimeOffset current, TimeSpan step)
        => span.Way == MetricWay.Future ? current : current - (step * (span.Count - 1));

    /// <summary>[S-16311] The hour under way at <paramref name="now"/>: its start, on the hour.</summary>
    public static DateTimeOffset HourOf(DateTimeOffset now)
        => new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);

    /// <summary>
    /// [S-16311] A place's hours (weather or air): a slot an hour of the span, each record on its hour's slot; the reading now
    /// the hour under way's. A temperature read from the hours (asked in °C, [S-12702]) is drawn in the shared scale.
    /// [S-16901] Slot 0 stands at its whole local hour (<see cref="HourNumber"/>); the <paramref name="lead"/> hours before
    /// the span are its <see cref="MetricSeries.Before"/>.
    /// </summary>
    public static MetricSeries Hours<THour>(MetricDef metric, MetricFeed feed, IReadOnlyList<THour> hours, Func<THour, DateTimeOffset> stamp,
        Func<THour, double?> value, Func<THour, double?>? second, MetricSpan span, DateTimeOffset now, TemperatureUnit unit, int lead = 0)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(hours);
        var step = TimeSpan.FromHours(1);
        var current = HourOf(now);
        var start = WindowStart(span, current, step);
        var count = Math.Max(2, span.Count);
        var slots = new MetricValue?[count];
        var before = new MetricValue?[Math.Max(0, lead)];
        MetricValue? reading = null;
        double Scaled(double v) => metric.Unit == MetricUnit.Temperature && unit == TemperatureUnit.Fahrenheit ? (v * 9 / 5) + 32 : v;

        foreach (var hour in hours)
        {
            if (value(hour) is not { } v || !double.IsFinite(v))
            {
                continue;
            }

            var s = second?.Invoke(hour) is { } sv && double.IsFinite(sv) ? Scaled(sv) : (double?)null;
            var item = new MetricValue(Scaled(v), feed.Shape == MetricShape.Pair ? s : null);
            var at = HourOf(stamp(hour).ToOffset(now.Offset));
            if (at == current)
            {
                reading = item;
            }

            var index = (int)Math.Round((at - start).TotalHours);
            if (index >= 0 && index < count)
            {
                slots[index] = item;
            }
            else if (index < 0 && index >= -before.Length)
            {
                before[before.Length + index] = item;
            }
        }

        return new MetricSeries(feed.Shape, slots, start, step) { Now = reading, Origin = HourNumber(start), Before = before };
    }

    /// <summary>[S-16901] A whole local hour's absolute number: the hours since the calendar's start on its own clock.</summary>
    public static long HourNumber(DateTimeOffset hour) => hour.DateTime.Ticks / TimeSpan.TicksPerHour;

    /// <summary>
    /// [S-16311] The forecast's days: a slot a day from today (the place's own calendar day the forecast starts on); today's
    /// the reading now. A pair's two are the day's low and high (or what the feed names). [S-16901] Slot 0 stands at its day's
    /// number; the <paramref name="lead"/> days before the span are its <see cref="MetricSeries.Before"/>.
    /// </summary>
    public static MetricSeries Days(MetricFeed feed, IReadOnlyList<ForecastDay> days, MetricSpan span, DateOnly today, TimeSpan offset, int lead = 0)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(days);
        var count = Math.Max(2, span.Count);
        var slots = new MetricValue?[count];
        var before = new MetricValue?[Math.Max(0, lead)];
        MetricValue? reading = null;
        var first = span.Way == MetricWay.Future ? today : today.AddDays(1 - count);
        foreach (var day in days)
        {
            if (feed.Day?.Invoke(day) is not { } v || !double.IsFinite(v))
            {
                continue;
            }

            var s = feed.Shape == MetricShape.Pair && feed.DaySecond?.Invoke(day) is { } sv && double.IsFinite(sv) ? sv : (double?)null;
            var item = new MetricValue(v, s);
            if (day.Day == today)
            {
                reading = item;
            }

            var index = day.Day.DayNumber - first.DayNumber;
            if (index >= 0 && index < count)
            {
                slots[index] = item;
            }
            else if (index < 0 && index >= -before.Length)
            {
                before[before.Length + index] = item;
            }
        }

        return new MetricSeries(feed.Shape, slots, new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), offset), TimeSpan.FromDays(1))
        {
            Now = reading,
            Origin = first.DayNumber,
            Before = before,
        };
    }

    /// <summary>[S-16311] A quote's period: a candle a bar (its own open, high, low and close, or the close alone), the last price now.</summary>
    public static MetricSeries Quote(TickerQuote quote, TimeSpan offset)
    {
        ArgumentNullException.ThrowIfNull(quote);
        var bars = quote.Bars;
        var slots = new MetricValue?[Math.Max(2, bars.Count)];
        var times = new DateTimeOffset[slots.Length];
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var candle = bar.Ohlc ?? Candle.Point(bar.Close);
            slots[i] = new MetricValue(bar.Close, Candle: candle);
            times[i] = DateTimeOffset.FromUnixTimeSeconds(bar.Time).ToOffset(offset);
        }

        var now = quote.Price is { } price && double.IsFinite(price) ? new MetricValue(price, Candle: Candle.Point(price)) : (MetricValue?)null;
        return new MetricSeries(MetricShape.Candle, slots, times[0], TimeSpan.FromMinutes(5)) { Now = now, Times = times };
    }

    /// <summary>[S-16311] A quote's period for a span back: the service's range that holds it.</summary>
    public static QuoteRange Range(MetricSpan span) => span.Count switch
    {
        <= 1 => QuoteRange.Day,
        <= 5 => QuoteRange.FiveDays,
        <= 31 => QuoteRange.Month,
        <= 92 => QuoteRange.ThreeMonths,
        <= 183 => QuoteRange.SixMonths,
        _ => QuoteRange.Year,
    };

    /// <summary>
    /// [S-16311] An exchange rate's working days within the span back from the newest (the ECB publishes no weekend), each at
    /// its day's noon; the newest the reading now. Null where the table has not both currencies.
    /// </summary>
    public static MetricSeries? Rates(RateTable table, string basis, string quote, MetricSpan span, TimeSpan offset)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (ExchangeRules.Cross(table, basis, quote) is not { Days: { } days } pair || days.Count != pair.History.Count || days.Count == 0)
        {
            return null;
        }

        var from = days[^1].AddDays(1 - Math.Max(1, span.Count));
        var kept = days.Select((day, i) => (Day: day, Rate: pair.History[i])).Where(d => d.Day >= from).ToList();
        if (kept.Count == 1)
        {
            kept.Insert(0, kept[0]);
        }

        return new MetricSeries(MetricShape.One, [.. kept.Select(d => (MetricValue?)new MetricValue(d.Rate))], PairRate.Noon(kept[0].Day, offset), TimeSpan.FromDays(1))
        {
            Now = new MetricValue(pair.Rate),
            Times = [.. kept.Select(d => PairRate.Noon(d.Day, offset))],
        };
    }
}
