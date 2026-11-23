using Themia.App.Services.Web;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Metrics.Tests;

/// <summary>[S-16300]..[S-16326] The Metric widget's rules: its catalog, its adapters, its graph's geometry, its face and its page.</summary>
public sealed class MetricsTests
{
    /// <summary>A whole hour, 2026-11-16 09:00 UTC, as the clock in these tests: 10:00 in Zürich.</summary>
    private static readonly DateTimeOffset Nine = DateTimeOffset.FromUnixTimeSeconds(1_763_283_600).ToOffset(TimeSpan.FromHours(1));

    private static readonly Locale English = Locale.En;

    // ---- the catalog ------------------------------------------------------------------------------

    [Fact]
    public void S16301_the_catalog_holds_everything_themia_reads_once_each_and_the_default_first()
    {
        string[] wanted =
        [
            "cpu", "gpu", "memory", "drive", "network", "ping", "battery", "temperature", "feelsLike", "precipitation", "snowfall",
            "precipitationChance", "wind", "humidity", "pressure", "uv", "cloudCover", "sunshine", "aqi", "pm25", "pm10", "ozone", "no2",
            "pollen", "pollenGrass", "stock", "exchangeRate",
        ];
        var ids = MetricCatalog.All.Select(m => m.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(wanted, id => Assert.Contains(id, ids));
        Assert.Equal(MetricCatalog.DefaultId, ids[0]);
        Assert.Same(MetricCatalog.All[0], MetricCatalog.Of("no such metric"));
        Assert.Null(MetricCatalog.Find("no such metric"));

        // In the line's order: by subject.
        Assert.Equal(MetricCatalog.All.OrderBy(m => m.Subject).Select(m => m.Id), ids);
    }

    [Fact]
    public void S16301_every_feed_reads_its_source_and_offers_spans_its_data_holds()
    {
        foreach (var metric in MetricCatalog.All)
        {
            Assert.NotEmpty(metric.Feeds);
            Assert.Equal(metric.Feeds.Count, metric.Feeds.Select(f => f.Step).Distinct().Count());
            foreach (var feed in metric.Feeds)
            {
                Assert.NotEmpty(feed.Spans);
                Assert.All(feed.Spans, s => Assert.Equal(feed.Step, s.Step));
                Assert.Contains(MetricCatalog.DefaultGraph(feed), MetricCatalog.Graphs(feed.Shape));
                switch (feed.Source)
                {
                    case MetricSource.Hub:
                        Assert.NotNull(feed.Hub);
                        Assert.All(feed.Spans, s => Assert.InRange(s.Count, 60, MeterTiming.MaxSpanSeconds));
                        break;
                    case MetricSource.Hours:
                        Assert.NotNull(feed.Hour);
                        Assert.Equal(feed.Shape == MetricShape.Pair, feed.HourSecond is not null);
                        Assert.All(feed.Spans, s => Assert.InRange(s.Count, 2, s.Way == MetricWay.Past ? WeatherHoursWire.PastHours : WeatherHoursWire.Hours - 1));
                        break;
                    case MetricSource.Days:
                        Assert.NotNull(feed.Day);
                        Assert.Equal(feed.Shape == MetricShape.Pair, feed.DaySecond is not null);
                        Assert.All(feed.Spans, s => Assert.InRange(s.Count, 2, WeatherReport.ForecastDays));
                        break;
                    case MetricSource.AirHours:
                        Assert.NotNull(feed.Air);
                        Assert.All(feed.Spans, s => Assert.InRange(s.Count, 2, s.Way == MetricWay.Past ? WeatherHoursWire.PastHours : WeatherHoursWire.AirHourCount - 1));
                        break;
                }
            }
        }
    }

    [Fact]
    public void S16301_the_combinations_are_pairs_and_a_price_a_candle()
    {
        Assert.Equal(MetricShape.Pair, MetricCatalog.Of("temperature").Feed(MetricStep.Day).Shape);
        Assert.Equal(MetricShape.One, MetricCatalog.Of("temperature").Feed(MetricStep.Hour).Shape);
        Assert.Equal(MetricShape.Pair, MetricCatalog.Of("drive").Feeds[0].Shape);
        Assert.Equal(MetricShape.Pair, MetricCatalog.Of("network").Feeds[0].Shape);
        Assert.Equal(MetricShape.Pair, MetricCatalog.Of("wind").Feed(MetricStep.Hour).Shape);
        Assert.Equal(MetricShape.Candle, MetricCatalog.Of("stock").Feeds[0].Shape);
        Assert.Equal([MetricStep.Hour, MetricStep.Day], MetricCatalog.Steps(MetricCatalog.Of("snowfall")));
        Assert.Equal(MetricGraphType.Range, MetricCatalog.DefaultGraph(MetricCatalog.Of("temperature").Feed(MetricStep.Day)));
        Assert.Equal(MetricGraphType.Candles, MetricCatalog.DefaultGraph(MetricCatalog.Of("stock").Feeds[0]));
    }

    [Fact]
    public void S16304_a_shape_offers_the_graphs_it_draws_and_no_other()
    {
        Assert.DoesNotContain(MetricGraphType.Candles, MetricCatalog.Graphs(MetricShape.One));
        Assert.DoesNotContain(MetricGraphType.Range, MetricCatalog.Graphs(MetricShape.One));
        Assert.Contains(MetricGraphType.Range, MetricCatalog.Graphs(MetricShape.Pair));
        Assert.DoesNotContain(MetricGraphType.Candles, MetricCatalog.Graphs(MetricShape.Pair));
        Assert.Contains(MetricGraphType.Candles, MetricCatalog.Graphs(MetricShape.Candle));
    }

    // ---- the adapters -----------------------------------------------------------------------------

    [Fact]
    public void S16311_the_hubs_points_stand_right_aligned_a_missed_probe_a_gap_and_a_pair_keeps_its_second()
    {
        var drive = MetricCatalog.Of("drive");
        SeriesPoint[] history = [new(10, 1), new(20, 2), new(0, Missed: true), new(40, double.NaN)];

        var series = MetricAdapters.Hub(drive, history, new MetricValue(40), new MetricSpan(MetricWay.Past, MetricStep.Live, 6), 1, Nine);

        Assert.Equal(6, series.Slots.Count);
        Assert.Null(series.Slots[0]);
        Assert.Null(series.Slots[1]);
        Assert.Equal(new MetricValue(10, 1), series.Slots[2]);
        Assert.Equal(new MetricValue(20, 2), series.Slots[3]);
        Assert.Null(series.Slots[4]); // missed: a gap, never a 0
        Assert.Equal(new MetricValue(40), series.Slots[5]); // no second: the value alone
        Assert.Equal(Nine, series.At(5));
        Assert.Equal(Nine.AddSeconds(-5), series.At(0));
        Assert.Equal(40, series.Now!.Value.Value);
    }

    [Fact]
    public void S16311_a_ping_span_is_its_points_five_seconds_apart()
    {
        Assert.Equal(60, MetricAdapters.HubSlots(new MetricSpan(MetricWay.Past, MetricStep.Live, 300), 5));
        Assert.Equal(300, MetricAdapters.HubSlots(new MetricSpan(MetricWay.Past, MetricStep.Live, 300), 1));
    }

    private static List<WeatherHour> Hours(int from, int count, Func<int, double?> temperature)
        => [.. Enumerable.Range(from, count).Select(i => new WeatherHour(Nine.AddHours(i)) { Temperature = temperature(i), Wind = 10 + i, Gusts = 20 + i })];

    [Fact]
    public void S16311_a_forecast_starts_at_the_hour_under_way_and_history_ends_there()
    {
        var metric = MetricCatalog.Of("temperature");
        var feed = metric.Feed(MetricStep.Hour);
        var hours = Hours(-24, 72, i => i == 3 ? null : i);
        var now = Nine.AddMinutes(42);

        var ahead = MetricAdapters.Hours(metric, feed, hours, h => h.At, feed.Hour!, null, new MetricSpan(MetricWay.Future, MetricStep.Hour, 24), now, TemperatureUnit.Celsius);
        var back = MetricAdapters.Hours(metric, feed, hours, h => h.At, feed.Hour!, null, new MetricSpan(MetricWay.Past, MetricStep.Hour, 12), now, TemperatureUnit.Celsius);

        Assert.Equal(24, ahead.Slots.Count);
        Assert.Equal(Nine, ahead.Start);
        Assert.Equal(0, ahead.Slots[0]!.Value.Value);
        Assert.Null(ahead.Slots[3]); // a figure the reply has not: a gap
        Assert.Equal(23, ahead.Slots[23]!.Value.Value);
        Assert.Equal(0, ahead.Now!.Value.Value);

        Assert.Equal(Nine.AddHours(-11), back.Start);
        Assert.Equal(-11, back.Slots[0]!.Value.Value);
        Assert.Equal(0, back.Slots[11]!.Value.Value);
    }

    [Fact]
    public void S16311_an_hourly_temperature_is_drawn_in_the_shared_scale_and_wind_keeps_its_gusts()
    {
        var temperature = MetricCatalog.Of("temperature");
        var feed = temperature.Feed(MetricStep.Hour);
        var span = new MetricSpan(MetricWay.Future, MetricStep.Hour, 12);
        var hours = Hours(0, 12, _ => 10);

        var f = MetricAdapters.Hours(temperature, feed, hours, h => h.At, feed.Hour!, null, span, Nine, TemperatureUnit.Fahrenheit);
        Assert.Equal(50, f.Now!.Value.Value, 6);

        var wind = MetricCatalog.Of("wind");
        var pair = wind.Feed(MetricStep.Hour);
        var w = MetricAdapters.Hours(wind, pair, hours, h => h.At, pair.Hour!, pair.HourSecond, span, Nine, TemperatureUnit.Fahrenheit);
        Assert.Equal(new MetricValue(11, 21), w.Slots[1]); // km/h whatever the temperature scale
    }

    [Fact]
    public void S16311_the_days_are_a_pair_of_low_and_high_from_today()
    {
        var metric = MetricCatalog.Of("temperature");
        var feed = metric.Feed(MetricStep.Day);
        var today = new DateOnly(2026, 11, 16);
        var days = Enumerable.Range(0, 14).Select(i => new ForecastDay(today.AddDays(i), 3, 2 + i, 10 + i, 0)).ToList();

        var week = MetricAdapters.Days(feed, days, new MetricSpan(MetricWay.Future, MetricStep.Day, 7), today, TimeSpan.FromHours(1));

        Assert.Equal(7, week.Slots.Count);
        Assert.Equal(new MetricValue(2, 10), week.Now);
        Assert.Equal(new MetricValue(8, 16), week.Slots[6]);
        Assert.Equal(2, week.Slots[0]!.Value.Low);
        Assert.Equal(10, week.Slots[0]!.Value.High);
    }

    [Fact]
    public void S16311_a_quote_is_a_candle_a_bar_and_its_range_holds_the_span()
    {
        var bars = new List<QuoteBar> { new(1000, 10), new(1300, 11, new Candle(10.5, 12, 10, 11)) };
        var quote = new TickerQuote("MSFT", "Microsoft", 11.2, null, "USD", 2, bars, TradingSessions.None);

        var series = MetricAdapters.Quote(quote, TimeSpan.Zero);

        Assert.Equal(new Candle(10, 10, 10, 10), series.Slots[0]!.Value.Candle);
        Assert.Equal(12, series.Slots[1]!.Value.High);
        Assert.Equal(11.2, series.Now!.Value.Value);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1300), series.At(1));
        Assert.Equal(QuoteRange.Day, MetricAdapters.Range(new MetricSpan(MetricWay.Past, MetricStep.Day, 1)));
        Assert.Equal(QuoteRange.ThreeMonths, MetricAdapters.Range(new MetricSpan(MetricWay.Past, MetricStep.Day, 90)));
        Assert.Equal(QuoteRange.Year, MetricAdapters.Range(new MetricSpan(MetricWay.Past, MetricStep.Day, 365)));
    }

    [Fact]
    public void S16311_a_rate_is_the_working_days_of_its_span_back_from_the_newest()
    {
        var newest = new DateOnly(2026, 11, 13);
        var days = Enumerable.Range(0, 60).Select(i => new RateDay(newest.AddDays(-i), new Dictionary<string, double> { ["USD"] = 1 + (i / 100.0) })).ToList();

        var series = MetricAdapters.Rates(new RateTable(days), "EUR", "USD", new MetricSpan(MetricWay.Past, MetricStep.Day, 30), TimeSpan.Zero)!;

        Assert.Equal(30, series.Slots.Count);
        Assert.Equal(1, series.Now!.Value.Value);
        Assert.Equal(1.29, series.Slots[0]!.Value.Value, 6);
        Assert.Null(MetricAdapters.Rates(new RateTable(days), "EUR", "JPY", new MetricSpan(MetricWay.Past, MetricStep.Day, 30), TimeSpan.Zero));
    }

    // ---- the graph --------------------------------------------------------------------------------

    private static MetricSeries Series(params double?[] values)
        => new(MetricShape.One, [.. values.Select(v => v is { } x ? new MetricValue(x) : (MetricValue?)null)], Nine, TimeSpan.FromHours(1));

    [Fact]
    public void S16312_slots_fold_into_runs_from_the_newest_by_mean_peak_or_sum()
    {
        var series = Series(1, 2, 3, 4, 5, 6, 7, null, null);

        var mean = MetricGraphRules.Fold(series, 3, MetricFold.Mean);
        var peak = MetricGraphRules.Fold(series, 3, MetricFold.Peak);
        var sum = MetricGraphRules.Fold(series, 3, MetricFold.Sum);

        Assert.Equal([2.0, 5.0], mean.Slots.Take(2).Select(s => s!.Value.Value));
        Assert.Equal(7, mean.Slots[2]!.Value.Value); // the newest run: 7 and two gaps
        Assert.Equal([3.0, 6.0, 7.0], peak.Slots.Select(s => s!.Value.Value));
        Assert.Equal([6.0, 15.0, 7.0], sum.Slots.Select(s => s!.Value.Value));
        Assert.Equal(Nine.AddHours(3), mean.At(1)); // a run stands at its first member's time
        Assert.Same(series, MetricGraphRules.Fold(series, 20, MetricFold.Mean));
        Assert.Null(MetricGraphRules.Fold(Series(1, null, null, null), 2, MetricFold.Mean).Slots[1]);
    }

    /// <summary>[S-16901] A folded series' runs by their absolute place (a run of its own times by its time) and their values.</summary>
    private static Dictionary<long, MetricValue?> Runs(MetricSeries folded)
        => folded.Times is { } times
            ? Enumerable.Range(0, folded.Slots.Count).ToDictionary(i => times[i].UtcTicks, i => folded.Slots[i])
            : Enumerable.Range(0, folded.Slots.Count).ToDictionary(i => folded.Origin + i, i => folded.Slots[i]);

    /// <summary>
    /// [S-16901] A series at successive moments, folded at <paramref name="width"/> in <paramref name="type"/>: it folds; every run
    /// that two moments both draw (but the newest, still filling) has one value; the runs drawn stay as many and move on by
    /// whole runs, most of them held from one moment to the next.
    /// </summary>
    private static void AssertSteady(IReadOnlyList<MetricSeries> moments, MetricGraphType type, double width, MetricFold fold, int slack = 0)
    {
        Dictionary<long, MetricValue?>? before = null;
        var moved = 0;
        int? runs = null;
        foreach (var series in moments)
        {
            var folded = MetricGraphRules.Fold(series, MetricGraphRules.MostSlots(type, series, width), fold);
            Assert.True(folded.Slots.Count < series.Slots.Count, $"{type}: folds");
            runs ??= folded.Slots.Count;
            Assert.InRange(folded.Slots.Count, runs.Value - slack, runs.Value + slack);
            var now = Runs(folded);
            if (before is not null)
            {
                var (newest, was) = (now.Keys.Max(), before.Keys.Max());
                var held = now.Keys.Where(k => k != newest && k != was && before.ContainsKey(k)).ToList();
                Assert.True(held.Count >= now.Count - 3 - slack, $"{type}: {held.Count} of {now.Count} runs held");
                Assert.All(held, k => Assert.Equal(before[k], now[k]));
                moved += newest != was ? 1 : 0;
            }

            before = now;
        }

        Assert.True(moved > 0, $"{type}: the graph moved on");
    }

    /// <summary>A reading a step, made up and never the same twice in a row.</summary>
    private static double Reading(long k) => ((k * 37) % 101) / 100.0;

    [Fact]
    public void S16901_a_hub_graphs_runs_keep_their_heights_as_points_arrive_in_every_graph_type()
    {
        var span = new MetricSpan(MetricWay.Past, MetricStep.Live, 300);
        var count = MetricAdapters.HubSlots(span, 1);
        foreach (var (id, types) in new[] { ("cpu", MetricCatalog.Graphs(MetricShape.One)), ("drive", MetricCatalog.Graphs(MetricShape.Pair)) })
        {
            var metric = MetricCatalog.Of(id);
            foreach (var type in types)
            {
                var lead = MetricGraphRules.Lead(count, MetricGraphRules.MaxSlots(type, 240));
                var moments = Enumerable.Range(1000, 25).Select(added =>
                {
                    var history = Enumerable.Range(added - count - lead, count + lead).Select(k => new SeriesPoint(Reading(k), Reading(k + 7))).ToList();
                    return MetricAdapters.Hub(metric, history, null, span, 1, Nine.AddSeconds(added), added);
                }).ToList();

                AssertSteady(moments, type, 240, metric.Feed(MetricStep.Live).Fold);
            }
        }
    }

    [Fact]
    public void S16901_a_places_hours_and_days_keep_their_runs_as_the_hour_and_the_day_move_on()
    {
        var metric = MetricCatalog.Of("temperature");
        var hourly = metric.Feed(MetricStep.Hour);
        var hours = Hours(-24, 140, i => Reading(i + 50) * 30);
        foreach (var way in new[] { MetricWay.Future, MetricWay.Past })
        {
            var span = new MetricSpan(way, MetricStep.Hour, way == MetricWay.Future ? 48 : 24);
            foreach (var type in MetricCatalog.Graphs(MetricShape.One))
            {
                var width = MetricGraphRules.Marks(type) ? 60 : 20; // ten marks: runs of five or three
                var lead = MetricGraphRules.Lead(span.Count, MetricGraphRules.MaxSlots(type, width));
                var moments = Enumerable.Range(0, 9).Select(h => MetricAdapters.Hours(metric, hourly, hours, x => x.At, hourly.Hour!, null, span,
                    Nine.AddHours(h).AddMinutes(20), TemperatureUnit.Celsius, lead)).ToList();
                AssertSteady(moments, type, width, hourly.Fold);
            }
        }

        var daily = metric.Feed(MetricStep.Day);
        var today = new DateOnly(2026, 11, 16);
        var days = Enumerable.Range(-5, 30).Select(i => new ForecastDay(today.AddDays(i), 3, Reading(i + 9) * 10, 10 + (Reading(i) * 10), 0)).ToList();
        var fortnight = new MetricSpan(MetricWay.Future, MetricStep.Day, 14);
        foreach (var type in MetricCatalog.Graphs(MetricShape.Pair))
        {
            var width = MetricGraphRules.Marks(type) ? 36 : 12;
            var lead = MetricGraphRules.Lead(14, MetricGraphRules.MaxSlots(type, width));
            var moments = Enumerable.Range(0, 6).Select(d => MetricAdapters.Days(daily, days, fortnight, today.AddDays(d), TimeSpan.FromHours(1), lead)).ToList();
            AssertSteady(moments, type, width, daily.Fold);
        }
    }

    [Fact]
    public void S16901_a_quotes_and_a_rates_runs_are_fixed_by_their_own_times()
    {
        // The ECB's working days, a new one each day, a span of ninety back.
        var newest = new DateOnly(2026, 11, 13);
        var all = Enumerable.Range(0, 200).Select(i => newest.AddDays(-i)).Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(d => new RateDay(d, new Dictionary<string, double> { ["USD"] = 1 + Reading(d.DayNumber) })).ToList();
        var back = new MetricSpan(MetricWay.Past, MetricStep.Day, 90);
        foreach (var type in MetricCatalog.Graphs(MetricShape.One))
        {
            var moments = Enumerable.Range(0, 12).Reverse().Select(skip => MetricAdapters.Rates(new RateTable([.. all.Skip(skip)]), "EUR", "USD", back, TimeSpan.Zero)!).ToList();
            AssertSteady(moments, type, MetricGraphRules.Marks(type) ? 120 : 40, MetricFold.Mean, slack: 1);
        }

        // A quote's five-minute bars over five sessions, a new bar each moment and the oldest gone.
        var open = DateTimeOffset.FromUnixTimeSeconds(1_763_040_600); // a Thursday 13:30 UTC
        var bars = Enumerable.Range(0, 7).SelectMany(day => Enumerable.Range(0, 78).Select(b => open.AddDays(day).AddMinutes(5 * b)))
            .Select((at, i) => new QuoteBar(at.ToUnixTimeSeconds(), 100 + (Reading(i) * 10))).ToList();
        foreach (var type in MetricCatalog.Graphs(MetricShape.Candle))
        {
            var moments = Enumerable.Range(0, 12).Select(i =>
                MetricAdapters.Quote(new TickerQuote("MSFT", "Microsoft", 101, null, "USD", 2, [.. bars.Skip(i).Take(390)], TradingSessions.None), TimeSpan.Zero)).ToList();
            AssertSteady(moments, type, MetricGraphRules.Marks(type) ? 240 : 80, MetricFold.Mean, slack: 2);
        }
    }

    [Fact]
    public void S16902_the_range_holds_while_the_readings_fit_it_and_a_share_is_always_its_hundred()
    {
        var held = MetricGraphRules.Steady(null, Series(2, 4, 6), fromZero: true);
        Assert.Equal(0, held.Low);
        Assert.True(held.High >= 6 * (1 + MetricGraphRules.Headroom));

        // The span moves on, its peak gone and a lower one in: the same range, so every height drawn stays.
        Assert.Equal(held, MetricGraphRules.Steady(held, Series(4, 6, 3), fromZero: true));
        Assert.Equal(held, MetricGraphRules.Steady(held, Series(3, 3, 4), fromZero: true));

        // A reading past its top refits it, roomy; readings under a third of it too.
        var grown = MetricGraphRules.Steady(held, Series(4, 6, 9), fromZero: true);
        Assert.True(grown.High >= 9);
        Assert.Equal(grown, MetricGraphRules.Steady(grown, Series(6, 9, 9.2), fromZero: true));
        Assert.True(MetricGraphRules.Steady(grown, Series(0.5, 1, 0.8), fromZero: true).High < 3);

        // A temperature's own low: held while inside it, a degree past its low refits.
        var warm = MetricGraphRules.Steady(null, Series(10, 20), fromZero: false);
        Assert.Equal(warm, MetricGraphRules.Steady(warm, Series(12, 19), fromZero: false));
        Assert.NotEqual(warm, MetricGraphRules.Steady(warm, Series(warm.Low - 1, 19), fromZero: false));

        Assert.Equal(new GraphRange(0, 1), MetricGraphRules.Steady(new GraphRange(0, 0.5), Series(0.1, 0.3), fromZero: true, share: true));
        Assert.Equal(2.5, MetricGraphRules.NiceStep(2.2));
        Assert.Equal(0.5, MetricGraphRules.NiceStep(0.41));
    }

    [Fact]
    public void S16312_candles_fold_into_their_runs_open_high_low_and_close()
    {
        var series = new MetricSeries(MetricShape.Candle,
            [new MetricValue(5, Candle: new Candle(4, 6, 3, 5)), new MetricValue(7, Candle: new Candle(5, 9, 5, 7))], Nine, TimeSpan.FromMinutes(5));

        var one = MetricGraphRules.Fold(series with { Slots = [.. series.Slots, .. series.Slots] }, 2, MetricFold.Mean);

        Assert.Equal(new Candle(4, 9, 3, 7), one.Slots[0]!.Value.Candle);
        Assert.Equal(7, one.Slots[0]!.Value.Value);
    }

    [Fact]
    public void S16312_a_plot_holds_a_line_point_every_two_dips_and_a_bar_every_six()
    {
        Assert.Equal(120, MetricGraphRules.MaxSlots(MetricGraphType.Line, 240));
        Assert.Equal(40, MetricGraphRules.MaxSlots(MetricGraphType.Bars, 240));
        Assert.Equal(24, MetricGraphRules.MaxSlots(MetricGraphType.Candles, 240));
        Assert.Equal(2, MetricGraphRules.MaxSlots(MetricGraphType.Line, 0));
    }

    [Fact]
    public void S16314_a_graph_with_slots_it_has_no_reading_for_draws_its_default_across_its_foot()
    {
        Assert.True(MetricGraphRules.DrawsDefault(Series(null, null, null)));
        Assert.True(MetricGraphRules.DrawsDefault(Series(null, null, 3))); // a history still filling: one point, the default under it
        Assert.False(MetricGraphRules.DrawsDefault(Series(1, 2, 3)));
        Assert.True(MetricGraphRules.DrawsDefault(MetricSeries.Empty(MetricShape.Pair, 60, Nine, TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void S16313_the_range_is_from_zero_for_an_amount_its_own_low_for_a_temperature_and_a_whole_hundred_for_a_share()
    {
        var amounts = MetricGraphRules.Range(Series(2, 4, 6), fromZero: true);
        var temperatures = MetricGraphRules.Range(Series(10, 20), fromZero: false);
        var share = MetricGraphRules.Range(Series(0.1, 0.3), fromZero: true, share: true);
        var flat = MetricGraphRules.Range(Series(5, 5), fromZero: false);

        Assert.Equal(0, amounts.Low);
        Assert.True(amounts.High > 6);
        Assert.True(temperatures.Low is < 10 and > 9);
        Assert.True(temperatures.High is > 20 and < 21);
        Assert.Equal(new GraphRange(0, 1), share);
        Assert.True(flat.Low < 5 && flat.High > 5);
        Assert.Equal(new GraphRange(0, 1), MetricGraphRules.Range(Series(null, null), fromZero: true));
    }

    [Fact]
    public void S16314_a_line_and_its_bars_reach_the_plots_edges_with_gaps_only_between_bars()
    {
        Assert.Equal(0, MetricGraphRules.LineX(0, 5, 200));
        Assert.Equal(200, MetricGraphRules.LineX(4, 5, 200));

        var first = MetricGraphRules.Bar(0, 10, 200);
        var last = MetricGraphRules.Bar(9, 10, 200);
        var second = MetricGraphRules.Bar(1, 10, 200);
        Assert.Equal(0, first.Left);
        Assert.Equal(200, last.Left + last.Width, 6);
        Assert.Equal(MetricGraphRules.BarGap, second.Left - (first.Left + first.Width), 6);
        Assert.Equal(first.Width, last.Width, 6);

        // A capped bar: the first still on the left edge, the last on the right, the rest evenly between.
        var capped = Enumerable.Range(0, 7).Select(i => MetricGraphRules.Bar(i, 7, 240, MetricGraphRules.RangeCap)).ToList();
        Assert.All(capped, b => Assert.Equal(MetricGraphRules.RangeCap, b.Width, 6));
        Assert.Equal(0, capped[0].Left);
        Assert.Equal(240, capped[^1].Left + capped[^1].Width, 6);
        Assert.Equal(capped[1].Left - capped[0].Left, capped[6].Left - capped[5].Left, 6);
    }

    [Fact]
    public void S16315_a_value_stands_down_its_range_and_the_pointer_finds_its_slot()
    {
        var range = new GraphRange(0, 10);
        Assert.Equal(100, MetricGraphRules.Y(0, range, 20, 100));
        Assert.Equal(20, MetricGraphRules.Y(10, range, 20, 100));
        Assert.Equal(60, MetricGraphRules.Y(5, range, 20, 100));
        Assert.Equal(100, MetricGraphRules.Y(-3, range, 20, 100)); // clamped to the plot

        Assert.Equal(0, MetricGraphRules.SlotAt(MetricGraphType.Line, 10, 5, 200));
        Assert.Equal(2, MetricGraphRules.SlotAt(MetricGraphType.Line, 100, 5, 200));
        Assert.Equal(4, MetricGraphRules.SlotAt(MetricGraphType.Line, 200, 5, 200));
        Assert.Equal(1, MetricGraphRules.SlotAt(MetricGraphType.Bars, 60, 5, 200));
        Assert.Equal(0, MetricGraphRules.SlotAt(MetricGraphType.Range, 2, 7, 240));
        Assert.Equal(6, MetricGraphRules.SlotAt(MetricGraphType.Range, 239, 7, 240));
        Assert.Null(MetricGraphRules.SlotAt(MetricGraphType.Bars, 201, 5, 200));

        // Over a gap the pointer reads the nearest reading.
        var slots = Series(1, null, null, 4).Slots;
        Assert.Equal(0, MetricGraphRules.Nearest(slots, 1));
        Assert.Equal(3, MetricGraphRules.Nearest(slots, 2));
        Assert.Null(MetricGraphRules.Nearest(Series(null, null).Slots, 0));
    }

    // ---- the face ---------------------------------------------------------------------------------

    [Fact]
    public void S16321_the_head_scales_with_the_card_within_its_least_and_most()
    {
        Assert.Equal(1, MetricFaceRules.Scale(240, 120));
        Assert.Equal(2, MetricFaceRules.Scale(480, 240));
        Assert.Equal(1, MetricFaceRules.Scale(480, 120)); // the smaller axis
        Assert.Equal(MetricFaceRules.LeastScale, MetricFaceRules.Scale(240, 40));
        Assert.Equal(MetricFaceRules.MostScale, MetricFaceRules.Scale(2000, 2000));
    }

    [Theory]
    [InlineData(240, 120)]
    [InlineData(480, 240)]
    [InlineData(330, 130)]
    [InlineData(900, 300)]
    public void S16321_the_heads_first_ink_stands_on_the_inset_and_the_plot_under_it_to_the_foot(double width, double height)
    {
        var face = MetricFaceRules.Graph(width, height);

        Assert.False(face.Behind);
        Assert.Equal(FamilySpace.Inset, face.HeadTop + (MetricFaceRules.HeadInk.Over * face.Scale), 6);
        Assert.True(face.PlotTop > face.HeadTop + face.HeadHeight);
        Assert.True(height - face.PlotTop >= MetricFaceRules.LeastPlot);
    }

    [Fact]
    public void S16322_a_card_too_low_for_head_and_graph_puts_the_graph_behind_the_head_its_whole_box()
    {
        var low = MetricFaceRules.Graph(240, 40);
        var tall = MetricFaceRules.Graph(240, 70);

        Assert.True(low.Behind);
        Assert.Equal(0, low.PlotTop);
        Assert.False(tall.Behind);

        // The threshold: the least plot under the head.
        for (var h = 30; h <= 120; h += 10)
        {
            var face = MetricFaceRules.Graph(240, h);
            Assert.Equal(h - (face.HeadTop + face.HeadHeight + (FamilySpace.S1 * face.Scale)) < MetricFaceRules.LeastPlot, face.Behind);
        }

        // In the middle where the inset does not fit over and under the head; the ink never higher than the inset.
        var lowest = MetricFaceRules.Graph(200, 30);
        Assert.True(lowest.HeadTop + (MetricFaceRules.HeadInk.Over * lowest.Scale) <= FamilySpace.Inset + 1e-9);
        Assert.False(MetricFaceRules.Graph(240, 40, graph: false).Behind);
        Assert.InRange(MetricFaceRules.BehindShare, 0.15, 0.5);
    }

    [Fact]
    public void S16329_a_click_opens_the_source_its_family_s_widgets_open()
    {
        var taskManager = System.IO.Path.Combine(Environment.SystemDirectory, "Taskmgr.exe");
        Assert.Equal(taskManager, new MetricsConfig { Metric = "cpu" }.Opens()?.Value);
        Assert.Equal(taskManager, new MetricsConfig { Metric = "memory" }.Opens()?.Value);
        Assert.Equal(taskManager, new MetricsConfig { Metric = "drive" }.Opens()?.Value);
        Assert.Equal("ms-settings:network-status", new MetricsConfig { Metric = "network" }.Opens()?.Value);
        Assert.Equal("ms-settings:network-status", new MetricsConfig { Metric = "ping" }.Opens()?.Value);
        Assert.Equal("ms-settings:batterysaver", new MetricsConfig { Metric = "battery" }.Opens()?.Value);

        // A place's metric: nothing without a place, its forecast with one.
        Assert.Null(new MetricsConfig { Metric = "temperature" }.Opens());
        Assert.NotNull(new MetricsConfig { Metric = "pm25", Latitude = 47.37, Longitude = 8.54, LocationName = "Zurich" }.Opens());

        // A stock its symbol's page, no line of its own on the menu; a rate nothing.
        Assert.Null(new MetricsConfig { Metric = "stock" }.Opens());
        var stock = new MetricsConfig { Metric = "stock", Symbol = "MSFT" };
        Assert.StartsWith("https://finance.yahoo.com/quote/MSFT", stock.Opens()?.Value, StringComparison.Ordinal);
        Assert.Null(stock.Opener);
        Assert.Null(new MetricsConfig { Metric = "exchangeRate", BaseCurrency = "EUR", QuoteCurrency = "USD" }.Opens());

        // Every metric of the catalog either opens through its family's line or is one of these.
        foreach (var def in MetricCatalog.All)
        {
            var config = new MetricsConfig { Metric = def.Id };
            Assert.Equal(def.Subject != MetricSubject.Money, config.Opener is not null);
        }
    }

    [Fact]
    public void S16903_the_value_face_is_one_row_the_name_at_the_left_the_value_at_the_right_centred_down_the_card()
    {
        var ink = MetricFaceRules.HeadInkHeight;

        // Wide and low: the height stops it; its ink on the insets over and under, the row from the left inset.
        var wide = MetricFaceRules.Row(600, 60, 150);
        Assert.Equal(FamilySpace.Inset, wide.Left);
        Assert.Equal((60 - (2 * FamilySpace.Inset)) / ink, wide.Scale, 6);
        Assert.Equal(FamilySpace.Inset, wide.Top + (MetricFaceRules.HeadInk.Over * wide.Scale), 6);

        // Tall: the width stops it (the row less the status mark's room); its ink centred down the card.
        var tall = MetricFaceRules.Row(200, 400, 150, room: 20);
        Assert.Equal((200 - (2 * FamilySpace.Inset) - 20) / 150.0, tall.Scale, 6);
        var inkTop = tall.Top + (MetricFaceRules.HeadInk.Over * tall.Scale);
        Assert.Equal(400 - inkTop - (ink * tall.Scale), inkTop, 6);

        // Never under the head's least; the row's width is the glyph, the name, the held box and their gaps.
        Assert.Equal(MetricFaceRules.LeastScale, MetricFaceRules.Row(40, 30, 150).Scale);
        Assert.Equal(FamilyShape.Icon + FamilySpace.GlyphGap + 50 + FamilySpace.ColumnGap + 60, MetricFaceRules.RowWidth(50, 60));
    }

    [Fact]
    public void S16324_a_reading_is_measured_by_its_widest_figures()
    {
        Assert.Equal("88%", MetricFaceRules.Template("12%"));
        Assert.Equal("8.8 MB/s", MetricFaceRules.Template("3.4 MB/s"));
        Assert.Equal("--", MetricFaceRules.Template("--"));
    }

    [Fact]
    public void S16325_each_metrics_least_is_where_its_name_and_widest_reading_read_at_the_floors()
    {
        static double Name(string text) => FamilyType.EstimateWidth(MetricFaceRules.Name, text.Length);

        foreach (var metric in MetricCatalog.All)
        {
            foreach (var style in Enum.GetValues<MetricsStyle>())
            {
                var config = new MetricsConfig { Style = style };
                config.Choose(metric.Id);
                var sizing = MetricsModule.Sized(config);
                Assert.True(sizing.Min.Width >= WidgetSizing.LeastScaled && sizing.Min.Height >= WidgetSizing.LeastScaled, metric.Id);
                Assert.True(sizing.Natural.Width >= sizing.Min.Width && sizing.Natural.Height >= sizing.Min.Height, metric.Id);
            }
        }

        // WR-14: the graph's least is the grid's least widget tall and as wide as the name and widest reading need at the floors:
        // a short metric narrower than a long one, neither a fixed width.
        var cpu = MetricsModule.Sized(new MetricsConfig()).Min;
        var drive = new MetricsConfig();
        drive.Choose("drive");
        Assert.Equal(WidgetSizing.LeastScaled, cpu.Height);
        Assert.True(cpu.Width < MetricsModule.Sized(drive).Min.Width);

        // The rule: at its least, the head holds the name whole and the reading at its least share (text at 9 DIP, glyph 12).
        var k = MetricFaceRules.LeastScale;
        var width = MetricFaceRules.GraphLeastWidth(Name("CPU"), 60);
        var room = ((width - (2 * FamilySpace.Inset) - MetricFaceRules.MarkRoom) / k) - MetricFaceRules.GlyphSize(k) - FamilySpace.GlyphGap - FamilySpace.ColumnGap;
        Assert.True(Name("CPU") + (60 * MetricFaceRules.LeastShare) <= room + 1e-6);
        Assert.Equal(ScaleFloor.Text, MetricFaceRules.Head.FontSize * k * MetricFaceRules.LeastShare, 6);
        Assert.True(MetricFaceRules.GraphLeastWidth(0, 0) >= WidgetSizing.LeastScaled);

        // At the least height the head stands over the graph behind it, its ink inside the card.
        var least = MetricFaceRules.Graph(cpu.Width, cpu.Height);
        Assert.True(least.Behind);
        var inkTop = least.HeadTop + (MetricFaceRules.HeadInk.Over * least.Scale);
        var inkFoot = least.HeadTop + least.HeadHeight - (MetricFaceRules.HeadInk.Under * least.Scale);
        Assert.True(inkTop > 0 && inkFoot < cpu.Height);

        // [S-16903] The value style's least is the head's row at its least: as wide as the graph's, one line of ink tall; its
        // natural the row at its own size, one line in its insets.
        var value = MetricFaceRules.Sizing(MetricsStyle.Value, Name("CPU"), 60);
        var graph = MetricFaceRules.Sizing(MetricsStyle.Graph, Name("CPU"), 60);
        Assert.Equal(graph.Min.Width, value.Min.Width);
        Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling((2 * FamilySpace.Inset) + (MetricFaceRules.HeadInkHeight * k))), value.Min.Height);
        Assert.Equal(Math.Ceiling((2 * FamilySpace.Inset) + MetricFaceRules.Head.LineHeight), value.Natural.Height);
        Assert.True(value.Natural.Width >= MetricFaceRules.ValueNaturalWidth);
        Assert.True(value.Natural.Width >= (2 * FamilySpace.Inset) + MetricFaceRules.RowWidth(Name("CPU"), 60));
    }

    [Fact]
    public void S16325_the_sizing_measures_names_as_wide_as_capitals_draw_and_both_styles_keep_the_marks_room()
    {
        // A name of capitals ("CPU", "MSFT") draws up to about 0.72 em a letter: the sizing's width holds it.
        Assert.True(MetricFaceRules.WordWidth(MetricFaceRules.Name, 3) >= 3 * 0.72 * MetricFaceRules.Name.FontSize);

        // [S-16903] The value face is the head's row: its least the graph's, the status mark's room beside the reading in both.
        var cpu = new MetricsConfig();
        Assert.Equal(MetricsModule.Sized(cpu).Min.Width, MetricsModule.Sized(new MetricsConfig { Style = MetricsStyle.Value }).Min.Width);
    }

    [Fact]
    public void S16321_the_readings_share_leaves_the_name_its_room_smoothly_down_to_the_names_size()
    {
        Assert.Equal(1, MetricFaceRules.ReadingShare(200, 80, 100));
        Assert.Equal(0.9, MetricFaceRules.ReadingShare(170, 80, 100), 6);
        Assert.Equal(MetricFaceRules.LeastShare, MetricFaceRules.ReadingShare(100, 80, 100));

        // Smooth: a cell more or less moves the share by a little, never by a step.
        for (var room = 120.0; room < 200; room += 10)
        {
            Assert.True(MetricFaceRules.ReadingShare(room + 10, 80, 100) - MetricFaceRules.ReadingShare(room, 80, 100) <= 0.1 + 1e-9);
        }
    }

    [Fact]
    public void S16327_why_is_a_word_the_status_line_reads_whole()
    {
        Assert.Equal("Offline", MetricFormat.Why(FetchStatus.Offline));
        Assert.Equal("No data", MetricFormat.Why(null));
        Assert.Equal("Unavailable", MetricFormat.Why(FetchStatus.Unreachable));
    }

    [Fact]
    public void S16324_a_metrics_widest_reading_is_its_largest_form_in_eights()
    {
        Assert.Equal("888%", MetricFormat.Widest(English, MetricCatalog.Of("cpu"), MetricShape.One));
        Assert.Equal("-88° / -88°", MetricFormat.Widest(English, MetricCatalog.Of("temperature"), MetricShape.Pair));
        Assert.Equal("888 / 888 MB/s", MetricFormat.Widest(English, MetricCatalog.Of("drive"), MetricShape.Pair));
        foreach (var metric in MetricCatalog.All)
        {
            foreach (var feed in metric.Feeds)
            {
                // Never narrower than a reading it shows: as many places as the format's largest, every figure an 8.
                var widest = MetricFormat.Widest(English, metric, feed.Shape);
                Assert.DoesNotContain(widest, c => char.IsAsciiDigit(c) && c != '8');
                Assert.NotEqual(MetricFormat.None, widest);
            }
        }
    }

    [Fact]
    public void S16312_candles_of_closes_alone_are_runs_of_four_at_every_size()
    {
        var closes = new MetricSeries(MetricShape.Candle, [.. Enumerable.Range(0, 78).Select(i => (MetricValue?)new MetricValue(i, Candle: Candle.Point(i)))], Nine, TimeSpan.FromMinutes(5));
        var real = closes with { Slots = [.. Enumerable.Range(0, 78).Select(i => (MetricValue?)new MetricValue(i, Candle: new Candle(i, i + 2, i - 1, i + 1)))] };

        Assert.Equal(20, MetricGraphRules.MostSlots(MetricGraphType.Candles, closes, 900));
        Assert.Equal(20, MetricGraphRules.MostSlots(MetricGraphType.Candles, closes, 480));
        Assert.Equal(18, MetricGraphRules.MostSlots(MetricGraphType.Candles, closes, 180)); // a narrow plot folds further
        Assert.Equal(90, MetricGraphRules.MostSlots(MetricGraphType.Candles, real, 900)); // real candles: as many as fit (all 78)
        Assert.Equal(450, MetricGraphRules.MostSlots(MetricGraphType.Line, closes, 900));

        // Folded, every candle has a body and a wick.
        var folded = MetricGraphRules.Fold(closes, MetricGraphRules.MostSlots(MetricGraphType.Candles, closes, 900), MetricFold.Mean);
        Assert.All(folded.Slots, s => Assert.True(s!.Value.Candle!.Value.High > s.Value.Candle.Value.Low));
    }

    [Fact]
    public void S16321_the_head_scales_down_to_its_text_read_at_9_and_its_glyph_at_12()
    {
        Assert.Equal(ScaleFloor.Text, MetricFaceRules.Name.FontSize * MetricFaceRules.LeastScale, 6);
        Assert.True(MetricFaceRules.LeastScale < 0.75);

        // The glyph never under 12 on the card: drawn larger in the head where the head is under 0.75.
        Assert.Equal(FamilyShape.Icon, MetricFaceRules.GlyphSize(1));
        Assert.Equal(FamilyShape.Icon, MetricFaceRules.GlyphSize(0.75), 6);
        Assert.Equal(ScaleFloor.Glyph, MetricFaceRules.GlyphSize(MetricFaceRules.LeastScale) * MetricFaceRules.LeastScale, 6);
    }

    // ---- formats ----------------------------------------------------------------------------------

    [Fact]
    public void S16303_a_pair_reads_both_and_a_share_its_percent()
    {
        Assert.Equal("8° / 16°", MetricFormat.Reading(English, MetricCatalog.Of("temperature"), new MetricValue(8.2, 15.6)));
        Assert.Equal("14 / 29 km/h", MetricFormat.Reading(English, MetricCatalog.Of("wind"), new MetricValue(14, 29)));
        Assert.Equal("3.4 / 0.3 MB/s", MetricFormat.Reading(English, MetricCatalog.Of("network"), new MetricValue(3.4 * 1024 * 1024, 0.28 * 1024 * 1024)));
        Assert.Equal("42%", MetricFormat.Reading(English, MetricCatalog.Of("cpu"), new MetricValue(0.42)));
        Assert.Equal("1.3 mm", MetricFormat.Reading(English, MetricCatalog.Of("precipitation"), new MetricValue(1.26)));
        Assert.Equal(MetricFormat.None, MetricFormat.Reading(English, MetricCatalog.Of("cpu"), null));
        Assert.Equal("418.62 USD", MetricFormat.Reading(English, MetricCatalog.Of("stock"), new MetricValue(418.62), "USD"));
    }

    [Fact]
    public void S16303_the_time_under_the_pointer_reads_in_its_steps_terms()
    {
        var hours = Series(1, 2, 3);
        var days = new MetricSeries(MetricShape.One, hours.Slots, Nine, TimeSpan.FromDays(1));

        Assert.Equal("11:00 AM", MetricFormat.When(English, hours, 1, Nine));
        Assert.Equal("Today", MetricFormat.When(English, days, 0, Nine));
    }

    // ---- the config -------------------------------------------------------------------------------

    [Fact]
    public void S16305_a_metric_chosen_starts_at_its_first_step_span_and_graph()
    {
        var config = new MetricsConfig();
        config.Choose("temperature");

        Assert.Equal((MetricStep.Hour, MetricWay.Future, 24, MetricGraphType.Line), (config.Step, config.Way, config.Span, config.Graph));

        config.ChooseStep(MetricStep.Day);
        Assert.Equal((MetricWay.Future, 7, MetricGraphType.Range), (config.Way, config.Span, config.Graph));

        config.ChooseStep(MetricStep.Hour);
        config.ChooseWay(MetricWay.Past);
        Assert.Equal((MetricWay.Past, 12), (config.SpanInForce.Way, config.SpanInForce.Count));
    }

    [Fact]
    public void S16305_repair_makes_an_unknown_metric_step_span_or_graph_its_default_loudly()
    {
        var config = new MetricsConfig { Metric = "nope" };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(MetricCatalog.DefaultId, config.Metric);

        var stepless = new MetricsConfig { Metric = "temperature", Step = MetricStep.Live };
        Assert.Single(stepless.Repair());
        Assert.Equal((MetricStep.Hour, 24, MetricGraphType.Line), (stepless.Step, stepless.Span, stepless.Graph));

        var odd = new MetricsConfig { Metric = "temperature", Step = MetricStep.Hour, Way = MetricWay.Future, Span = 5, Graph = MetricGraphType.Candles };
        Assert.Equal(2, odd.Repair().Count(n => n.StartsWith("metrics:", StringComparison.Ordinal)));
        Assert.Equal(24, odd.Span);
        Assert.Equal(MetricGraphType.Line, odd.Graph);
        Assert.Empty(new MetricsConfig().Repair());
    }

    [Fact]
    public void S16305_a_clone_carries_every_setting()
    {
        var config = new MetricsConfig
        {
            Metric = "stock", Step = MetricStep.Day, Way = MetricWay.Past, Span = 90, Graph = MetricGraphType.Line, Style = MetricsStyle.Value,
            Scope = DiskScope.AllDrives, Drive = "D:\\", Adapter = "{x}", AdapterName = "Wi-Fi", Host = "example.org", GpuAdapter = 1,
            Symbol = "MSFT", Market = QuoteMarket.Listed, DisplayName = "Microsoft", BaseCurrency = "CHF", QuoteCurrency = "USD",
            Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich",
        };

        var copy = (MetricsConfig)config.Clone();

        Assert.NotSame(config, copy);
        foreach (var property in typeof(MetricsConfig).GetProperties().Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
        {
            Assert.Equal(property.GetValue(config), property.GetValue(copy));
        }
    }

    [Fact]
    public void S16306_it_waits_for_setup_only_for_what_its_metric_needs()
    {
        static bool Waits(MetricsConfig c) => ((ISetupRule)c).NeedsSetup(FeatureSet.None);

        Assert.False(Waits(new MetricsConfig()));
        Assert.True(Waits(new MetricsConfig { Metric = "temperature" }));
        Assert.False(Waits(new MetricsConfig { Metric = "temperature", Latitude = 1, Longitude = 2 }));
        Assert.True(Waits(new MetricsConfig { Metric = "stock" }));
        Assert.False(Waits(new MetricsConfig { Metric = "stock", Symbol = "MSFT" }));
        Assert.False(Waits(new MetricsConfig { Metric = "exchangeRate" }));
    }

    [Fact]
    public void S16340_it_holds_the_shared_setting_its_metric_reads_and_no_other()
    {
        var cpu = new MetricsConfig();
        var weather = new MetricsConfig { Metric = "uv" };
        var drive = new MetricsConfig { Metric = "drive" };

        Assert.DoesNotContain(SharedSettings.All, s => s.Holds(cpu));
        Assert.Equal([SharedSettings.Place], SharedSettings.All.Where(s => s.Holds(weather)));
        Assert.Equal([SharedSettings.DiskScope], SharedSettings.All.Where(s => s.Holds(drive)));
        Assert.Equal([SharedSettings.PingHost], SharedSettings.All.Where(s => s.Holds(new MetricsConfig { Metric = "ping" })));
    }

    [Fact]
    public void S16307_it_asks_only_for_what_its_metric_reads()
    {
        static InterestsBuilder Of(MetricsConfig config)
        {
            var interests = new InterestsBuilder(new RefreshPreferences(), TemperatureUnit.Celsius);
            config.AddInterests(interests);
            return interests;
        }

        var placed = (double Lat, double Lon) => new MetricsConfig { Latitude = 47.37, Longitude = 8.54 };

        Assert.Empty(Of(new MetricsConfig()).Of<HoursInterest>());
        var hourly = placed(0, 0);
        hourly.Choose("uv");
        Assert.Single(Of(hourly).Of<HoursInterest>());
        Assert.Empty(Of(hourly).Of<WeatherInterest>());
        hourly.ChooseStep(MetricStep.Day);
        Assert.Equal(WeatherReport.ForecastDays, Assert.Single(Of(hourly).Of<WeatherInterest>()).ForecastDays);
        Assert.Empty(Of(hourly).Of<HoursInterest>());

        var pollen = placed(0, 0);
        pollen.Choose("pollen");
        Assert.Single(Of(pollen).Of<AirHoursInterest>());
        Assert.Empty(Of(new MetricsConfig { Metric = "pollen" }).Of<AirHoursInterest>()); // no place: nothing asked

        var stock = new MetricsConfig { Symbol = "MSFT" };
        stock.Choose("stock");
        stock.Span = 90;
        Assert.Equal(QuoteRange.ThreeMonths, Assert.Single(Of(stock).Of<QuoteInterest>()).Range);

        // The hub: its series alone, a drive's with its reads and writes apart.
        Assert.Equal(SystemInterests.Chart(SystemSource.Cpu).Series, new MetricsConfig().SystemWants("C:\\").Series);
        Assert.Equal(DiskScopes.Apart("C:\\"), new MetricsConfig { Metric = "drive" }.Series("C:\\")!.Value.Instance);
        Assert.Equal(SystemInterests.None, hourly.SystemWants("C:\\"));
    }

    // ---- the page ---------------------------------------------------------------------------------

    private static readonly MetricsMenuSources Sources = new(
        () => [new("C:\\", VolumeKind.Fixed, DriveState.Ready, "System")], () => [], () => [], (_, _) => Task.FromResult<IReadOnlyList<SymbolHit>>([]));

    private static WidgetMenuLines Page(MetricsConfig config)
    {
        var menu = new WidgetMenuLines(() => { });
        MetricsMenu.Lines(menu, config, Sources);
        return menu;
    }

    [Fact]
    public void S16308_the_page_is_the_subject_the_metric_its_source_and_its_spans_with_the_graph_under_style()
    {
        var hourly = new MetricsConfig { Latitude = 1, Longitude = 2 };
        hourly.Choose("temperature");
        var page = Page(hourly);
        var texts = page.Lines.Select(l => l.Text).ToList();

        Assert.Equal(MetricsStr.Metrics_Menu_Subject.Text(), texts[0]);
        Assert.Equal(MetricsStr.Metrics_Menu_Metric.Text(), texts[1]);
        Assert.Contains(page.Lines, l => l.Shares == SharedSettings.Place.Key);
        Assert.Contains(MetricsStr.Metrics_Menu_Step.Text(), texts);
        Assert.Contains(MetricsStr.Metrics_Menu_Way.Text(), texts);
        Assert.Contains(MetricsStr.Metrics_Menu_Span.Text(), texts);
        var graph = Assert.IsType<SettingsChoice>(Assert.Single(page.StyleLines));
        Assert.Equal(MetricCatalog.Graphs(MetricShape.One).Count, graph.Options.Count);

        // The days look ahead only: no way to choose; the value style has no graph to choose.
        hourly.ChooseStep(MetricStep.Day);
        Assert.DoesNotContain(MetricsStr.Metrics_Menu_Way.Text(), Page(hourly).Lines.Select(l => l.Text));
        hourly.Style = MetricsStyle.Value;
        Assert.Empty(Page(hourly).StyleLines);

        // A long subject's metrics are found by typing; the PC's need no box.
        Assert.NotNull(Assert.IsType<SettingsChoice>(Page(new MetricsConfig { Metric = "pm25" }).Lines[1]).Filter);
        Assert.Null(Assert.IsType<SettingsChoice>(Page(new MetricsConfig()).Lines[1]).Filter);

        // The CPU reads the PC as a whole: no source line, nothing shared.
        Assert.DoesNotContain(Page(new MetricsConfig()).Lines, l => l.Shares is not null);
    }

    [Fact]
    public void S16308_a_drive_marks_its_shared_line_and_a_stock_its_symbol_unmarked()
    {
        Assert.Contains(Page(new MetricsConfig { Metric = "drive" }).Lines, l => l.Shares == SharedSettings.DiskScope.Key);

        var stock = Page(new MetricsConfig { Metric = "stock" });
        Assert.Contains(stock.Lines, l => l is SettingsSearch);
        Assert.All(stock.Lines, l => Assert.Null(l.Shares));
    }

    [Fact]
    public void S16308_picking_a_subject_shows_its_first_metric()
    {
        var config = new MetricsConfig();
        var subject = Assert.IsType<SettingsChoice>(Page(config).Lines[0]);

        subject.Options.Single(o => o.Text == MetricsStr.Metrics_Subject_Money.Text()).Pick();

        Assert.Equal("stock", config.Metric);
        Assert.Equal(MetricStep.Day, config.Step);
    }

    [Fact]
    public void S16900_spans_are_listed_lowest_first_everywhere_and_the_default_stands_where_it_falls()
    {
        foreach (var feed in MetricCatalog.All.SelectMany(m => m.Feeds))
        {
            Assert.Contains(feed.Default, feed.Spans);
            foreach (var way in MetricCatalog.Ways(feed))
            {
                var lengths = feed.Spans.Where(s => s.Way == way).Select(s => s.Length()).ToList();
                Assert.Equal(lengths.Order(), lengths);
                Assert.Equal(lengths.Order(), MetricCatalog.Spans(feed, way).Select(s => s.Length()));
            }
        }

        // The page's Span line in the same order: a minute first, the five-minute default where it falls.
        var cpu = new MetricsConfig();
        Assert.Equal((MetricWay.Past, 300), (cpu.SpanInForce.Way, cpu.SpanInForce.Count));
        var line = Assert.IsType<SettingsChoice>(Page(cpu).Lines.Single(l => l.Text == MetricsStr.Metrics_Menu_Span.Text()));
        Assert.Equal(MetricCatalog.Spans(cpu.Feed, MetricWay.Past).Select(MetricsMenu.SpanLabel), line.Options.Select(o => o.Text));

        var hourly = new MetricsConfig();
        hourly.Choose("temperature");
        Assert.Equal((MetricWay.Future, 24), (hourly.Way, hourly.Span));
        Assert.Equal([12, 24, 48], MetricCatalog.Spans(hourly.Feed, MetricWay.Future).Select(s => s.Count));
    }
}
