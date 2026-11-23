using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.Metrics;

/// <summary>[S-16301] The subjects the metric line groups its metrics under, in the line's order.</summary>
public enum MetricSubject
{
    System = 0,
    Weather = 1,
    Air = 2,
    Money = 3,
}

/// <summary>[S-16301] What one point of a metric's series holds: one value, a pair (low and high, read and write), or a candle.</summary>
public enum MetricShape
{
    One = 0,
    Pair = 1,
    Candle = 2,
}

/// <summary>[S-16301] Where a metric's series comes from: the host's data, each read through its own adapter.</summary>
public enum MetricSource
{
    /// <summary>The system hub's charted series (<c>ISystemFeeds.WatchSeries</c>), a point a period.</summary>
    Hub = 0,

    /// <summary>open-meteo's hourly weather at a place (<c>IWeatherAnswers.Hours</c>): a day back, two days ahead.</summary>
    Hours = 1,

    /// <summary>open-meteo's daily forecast at a place (<c>IWeatherAnswers.Forecast</c>): up to fourteen days.</summary>
    Days = 2,

    /// <summary>open-meteo's hourly air quality at a place (<c>IWeatherAnswers.AirHours</c>): a day back, four days ahead.</summary>
    AirHours = 3,

    /// <summary>A quote's bars (<c>IQuoteAnswers.Quote</c>).</summary>
    Quote = 4,

    /// <summary>The ECB's 90-day reference rates (<c>IRateAnswers.Rates</c>).</summary>
    Rates = 5,
}

/// <summary>[S-16301] What a metric needs set before it can read: its source line on the page.</summary>
public enum MetricNeed
{
    None = 0,
    Place = 1,
    Drive = 2,
    GpuAdapter = 3,
    NetworkAdapter = 4,
    PingHost = 5,
    Symbol = 6,
    CurrencyPair = 7,
}

/// <summary>[S-16302] One step of a series: a slot each.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetricStep>))]
public enum MetricStep
{
    /// <summary>The hub's period: a second (a ping's five).</summary>
    Live = 0,
    Hour = 1,
    Day = 2,
}

/// <summary>[S-16302] Which way the graph looks from now.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetricWay>))]
public enum MetricWay
{
    /// <summary>History: the span up to now, now at the right edge.</summary>
    Past = 0,

    /// <summary>Forecast: the span from now, now at the left edge.</summary>
    Future = 1,
}

/// <summary>[S-16303] How a value reads: its unit and its digits.</summary>
public enum MetricUnit
{
    /// <summary>A share, 0..1, read as a percentage.</summary>
    Share = 0,

    /// <summary>0..100, read as a percentage.</summary>
    Percent = 1,

    /// <summary>Bytes a second.</summary>
    Rate = 2,
    Milliseconds = 3,

    /// <summary>Degrees in the shared scale.</summary>
    Temperature = 4,
    Millimetres = 5,
    Centimetres = 6,

    /// <summary>km/h.</summary>
    Speed = 7,

    /// <summary>hPa.</summary>
    Pressure = 8,

    /// <summary>A plain index (UV, AQI), whole.</summary>
    Index = 9,

    /// <summary>µg/m³.</summary>
    Micrograms = 10,

    /// <summary>Pollen grains/m³.</summary>
    Grains = 11,

    /// <summary>Minutes (of sunshine in an hour).</summary>
    Minutes = 12,

    /// <summary>A price in the quote's currency.</summary>
    Price = 13,

    /// <summary>An exchange rate.</summary>
    Ratio = 14,
}

/// <summary>[S-16304] The graph types; a metric offers those its shape draws (<see cref="MetricCatalog.Graphs"/>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetricGraphType>))]
public enum MetricGraphType
{
    Area = 0,
    Line = 1,
    Bars = 2,
    Step = 3,

    /// <summary>A bar from the low to the high: a pair's.</summary>
    Range = 4,

    /// <summary>Open, high, low and close: a candle's.</summary>
    Candles = 5,
}

/// <summary>[S-16302] One span a metric offers: which way, at what step, how many steps.</summary>
public readonly record struct MetricSpan(MetricWay Way, MetricStep Step, int Count)
{
    /// <summary>The span's length in time: <paramref name="period"/> a live step (the hub's period).</summary>
    public TimeSpan Length(int period = 1) => Step switch
    {
        MetricStep.Hour => TimeSpan.FromHours(Count),
        MetricStep.Day => TimeSpan.FromDays(Count),
        _ => TimeSpan.FromSeconds(Count),
    };
}

/// <summary>
/// [S-16301] One way a metric is read: from its source at a step, in a shape, over the spans offered (the first the
/// default), and what of each record of the source is its value. A metric has one feed per step it has (temperature: the
/// hours and the days, a pair of low and high by day).
/// </summary>
public sealed record MetricFeed(MetricSource Source, MetricStep Step, MetricShape Shape, IReadOnlyList<MetricSpan> Spans)
{
    /// <summary>An hour's value (and its second: gusts over wind).</summary>
    public Func<WeatherHour, double?>? Hour { get; init; }

    public Func<WeatherHour, double?>? HourSecond { get; init; }

    /// <summary>A day's value (and its second: the high over the low).</summary>
    public Func<ForecastDay, double?>? Day { get; init; }

    public Func<ForecastDay, double?>? DaySecond { get; init; }

    /// <summary>An air hour's value.</summary>
    public Func<AirHour, double?>? Air { get; init; }

    /// <summary>The hub's source charted.</summary>
    public SystemSource? Hub { get; init; }

    /// <summary>
    /// [S-16312] How slots fold into one when a graph has fewer than its span holds: the peak for a rate or a latency
    /// (a burst stays seen), the sum for an amount (rain, snow), else the mean.
    /// </summary>
    public MetricFold Fold { get; init; } = MetricFold.Mean;

    /// <summary>Its graph by default (the first of the types its shape offers otherwise).</summary>
    public MetricGraphType? Graph { get; init; }
}

/// <summary>[S-16312] How slots fold.</summary>
public enum MetricFold
{
    Mean = 0,
    Peak = 1,
    Sum = 2,
}

/// <summary>
/// [S-16301] A metric: its id (what the config stores), subject, name, glyph, unit and digits, what it needs set, whether it
/// has a reading now, and its feeds. Adding a metric is one entry here (and an adapter where its source is new): nothing
/// in the view.
/// </summary>
public sealed record MetricDef(string Id, MetricSubject Subject, LocalText Name, string Glyph, MetricUnit Unit, MetricNeed Need, IReadOnlyList<MetricFeed> Feeds)
{
    /// <summary>Digits after the point its value reads with.</summary>
    public int Digits { get; init; }

    /// <summary>[S-16303] Whether the graph's foot is zero (an amount, a share) or the series' own low (a temperature, a pressure).</summary>
    public bool FromZero { get; init; } = true;

    /// <summary>The feed at <paramref name="step"/>, or the first.</summary>
    public MetricFeed Feed(MetricStep step) => Feeds.FirstOrDefault(f => f.Step == step) ?? Feeds[0];

    /// <summary>Every span it offers, its feeds' in order.</summary>
    public IEnumerable<MetricSpan> Spans => Feeds.SelectMany(f => f.Spans);
}

/// <summary>
/// [S-16301] Every metric the widget offers: everything Themia already reads (the system hub's charted series, the place's
/// hours and days, its air hours, a quote and an exchange rate), one entry each. Pure data and lookups, tested.
/// </summary>
public static class MetricCatalog
{
    /// <summary>The metric a new widget shows, and the one an unknown stored id becomes.</summary>
    public const string DefaultId = "cpu";

    /// <summary>The hub's spans: a minute to an hour back (its history holds an hour, <c>MeterTiming.MaxSpanSeconds</c>).</summary>
    private static readonly MetricSpan[] LiveSpans = [Live(300), Live(60), Live(900), Live(1800), Live(3600)];

    /// <summary>A day ahead first, then two; half a day and a day back (the hours a reply carries).</summary>
    private static readonly MetricSpan[] HourSpans = [Ahead(MetricStep.Hour, 24), Ahead(MetricStep.Hour, 12), Ahead(MetricStep.Hour, 48), Back(MetricStep.Hour, 12), Back(MetricStep.Hour, 24)];

    /// <summary>A week and two ahead (the forecast's fourteen days).</summary>
    private static readonly MetricSpan[] DaySpans = [Ahead(MetricStep.Day, 7), Ahead(MetricStep.Day, 14)];

    /// <summary>The air: a day ahead first, two and four; a day back.</summary>
    private static readonly MetricSpan[] AirSpans = [Ahead(MetricStep.Hour, 24), Ahead(MetricStep.Hour, 48), Ahead(MetricStep.Hour, 96), Back(MetricStep.Hour, 24)];

    /// <summary>A quote's periods, back: a day, five, a month, three, six, a year (the service's ranges).</summary>
    private static readonly MetricSpan[] QuoteSpans = [Back(MetricStep.Day, 1), Back(MetricStep.Day, 5), Back(MetricStep.Day, 30), Back(MetricStep.Day, 90), Back(MetricStep.Day, 180), Back(MetricStep.Day, 365)];

    /// <summary>The ECB's working days, back: a month and the ninety days it publishes.</summary>
    private static readonly MetricSpan[] RateSpans = [Back(MetricStep.Day, 30), Back(MetricStep.Day, 90)];

    private static MetricSpan Live(int seconds) => new(MetricWay.Past, MetricStep.Live, seconds);

    private static MetricSpan Ahead(MetricStep step, int count) => new(MetricWay.Future, step, count);

    private static MetricSpan Back(MetricStep step, int count) => new(MetricWay.Past, step, count);

    private static MetricFeed Hub(SystemSource source, MetricShape shape = MetricShape.One, MetricFold fold = MetricFold.Mean, MetricGraphType? graph = null)
        => new(MetricSource.Hub, MetricStep.Live, shape, LiveSpans) { Hub = source, Fold = fold, Graph = graph };

    private static MetricFeed Hourly(Func<WeatherHour, double?> value, MetricFold fold = MetricFold.Mean, MetricGraphType? graph = null)
        => new(MetricSource.Hours, MetricStep.Hour, MetricShape.One, HourSpans) { Hour = value, Fold = fold, Graph = graph };

    private static MetricFeed Daily(Func<ForecastDay, double?> value, MetricGraphType? graph = MetricGraphType.Bars, MetricFold fold = MetricFold.Mean)
        => new(MetricSource.Days, MetricStep.Day, MetricShape.One, DaySpans) { Day = value, Fold = fold, Graph = graph };

    private static MetricFeed AirHourly(Func<AirHour, double?> value, MetricGraphType? graph = null)
        => new(MetricSource.AirHours, MetricStep.Hour, MetricShape.One, AirSpans) { Air = value, Graph = graph };

    private static MetricDef Pollen(string id, LocalText name, PollenType? type)
        => new(id, MetricSubject.Air, name, Glyphs.Weather, MetricUnit.Grains, MetricNeed.Place,
            [AirHourly(h => type is { } one ? (h.Pollen.TryGetValue(one, out var grains) ? grains : null) : (h.Pollen.Count > 0 ? h.Pollen.Values.Max() : null), MetricGraphType.Bars)]);

    /// <summary>Every metric, in the line's order: by subject, then as Themia's widgets show them.</summary>
    public static IReadOnlyList<MetricDef> All { get; } =
    [
        // ---- the system hub: a point a second, an hour of history
        new("cpu", MetricSubject.System, MetricsStr.Metrics_Metric_Cpu.Local(), Glyphs.Cpu, MetricUnit.Share, MetricNeed.None, [Hub(SystemSource.Cpu)]),
        new("gpu", MetricSubject.System, MetricsStr.Metrics_Metric_Gpu.Local(), Glyphs.Gpu, MetricUnit.Share, MetricNeed.GpuAdapter, [Hub(SystemSource.Gpu)]),
        new("memory", MetricSubject.System, MetricsStr.Metrics_Metric_Memory.Local(), Glyphs.Memory, MetricUnit.Share, MetricNeed.None, [Hub(SystemSource.Memory)]),
        new("drive", MetricSubject.System, MetricsStr.Metrics_Metric_Drive.Local(), Glyphs.Disk, MetricUnit.Rate, MetricNeed.Drive,
            [Hub(SystemSource.DiskActivity, MetricShape.Pair, MetricFold.Peak, MetricGraphType.Line)]),
        new("network", MetricSubject.System, MetricsStr.Metrics_Metric_Network.Local(), Glyphs.Network, MetricUnit.Rate, MetricNeed.NetworkAdapter,
            [Hub(SystemSource.Network, MetricShape.Pair, MetricFold.Peak, MetricGraphType.Line)]),
        new("ping", MetricSubject.System, MetricsStr.Metrics_Metric_Ping.Local(), Glyphs.Ping, MetricUnit.Milliseconds, MetricNeed.PingHost, [Hub(SystemSource.Ping, fold: MetricFold.Peak, graph: MetricGraphType.Line)]),
        new("battery", MetricSubject.System, MetricsStr.Metrics_Metric_Battery.Local(), Glyphs.Battery, MetricUnit.Share, MetricNeed.None, [Hub(SystemSource.Battery)]),

        // ---- the weather at a place: the hours (a day back, two ahead) and the days (two weeks ahead)
        new("temperature", MetricSubject.Weather, MetricsStr.Metrics_Metric_Temperature.Local(), Glyphs.Thermometer, MetricUnit.Temperature, MetricNeed.Place,
            [
                Hourly(h => h.Temperature, graph: MetricGraphType.Line),
                new(MetricSource.Days, MetricStep.Day, MetricShape.Pair, DaySpans) { Day = d => d.Low, DaySecond = d => d.High, Graph = MetricGraphType.Range },
            ]) { FromZero = false },
        new("feelsLike", MetricSubject.Weather, MetricsStr.Metrics_Metric_FeelsLike.Local(), Glyphs.Thermometer, MetricUnit.Temperature, MetricNeed.Place,
            [Hourly(h => h.FeelsLike, graph: MetricGraphType.Line)]) { FromZero = false },
        new("precipitation", MetricSubject.Weather, MetricsStr.Metrics_Metric_Precipitation.Local(), Glyphs.Rain, MetricUnit.Millimetres, MetricNeed.Place,
            [Hourly(h => h.Precipitation, MetricFold.Sum, MetricGraphType.Bars), Daily(d => d.Precipitation, fold: MetricFold.Sum)]) { Digits = 1 },
        new("snowfall", MetricSubject.Weather, MetricsStr.Metrics_Metric_Snowfall.Local(), Glyphs.Snow, MetricUnit.Centimetres, MetricNeed.Place,
            [Hourly(h => h.Snowfall, MetricFold.Sum, MetricGraphType.Bars), Daily(d => d.Snowfall, fold: MetricFold.Sum)]) { Digits = 1 },
        new("precipitationChance", MetricSubject.Weather, MetricsStr.Metrics_Metric_PrecipitationChance.Local(), Glyphs.Droplet, MetricUnit.Percent, MetricNeed.Place,
            [Hourly(h => h.PrecipitationChance, graph: MetricGraphType.Bars), Daily(d => d.PrecipitationChance)]),
        new("wind", MetricSubject.Weather, MetricsStr.Metrics_Metric_Wind.Local(), Glyphs.Wind, MetricUnit.Speed, MetricNeed.Place,
            [
                new(MetricSource.Hours, MetricStep.Hour, MetricShape.Pair, HourSpans) { Hour = h => h.Wind, HourSecond = h => h.Gusts, Graph = MetricGraphType.Line },
                Daily(d => d.WindMax),
            ]),
        new("humidity", MetricSubject.Weather, MetricsStr.Metrics_Metric_Humidity.Local(), Glyphs.Droplet, MetricUnit.Percent, MetricNeed.Place, [Hourly(h => h.Humidity)]),
        new("pressure", MetricSubject.Weather, MetricsStr.Metrics_Metric_Pressure.Local(), Glyphs.Gauge, MetricUnit.Pressure, MetricNeed.Place,
            [Hourly(h => h.Pressure, graph: MetricGraphType.Line)]) { FromZero = false },
        new("uv", MetricSubject.Weather, MetricsStr.Metrics_Metric_Uv.Local(), Glyphs.Sun, MetricUnit.Index, MetricNeed.Place,
            [Hourly(h => h.Uv, graph: MetricGraphType.Bars), Daily(d => d.UvMax)]) { Digits = 0 },
        new("cloudCover", MetricSubject.Weather, MetricsStr.Metrics_Metric_CloudCover.Local(), Glyphs.Cloudy, MetricUnit.Percent, MetricNeed.Place, [Hourly(h => h.CloudCover)]),
        new("sunshine", MetricSubject.Weather, MetricsStr.Metrics_Metric_Sunshine.Local(), Glyphs.Sun, MetricUnit.Minutes, MetricNeed.Place,
            [Hourly(h => h.Sunshine, MetricFold.Sum, MetricGraphType.Bars)]),

        // ---- the air at a place, hour by hour
        new("aqi", MetricSubject.Air, MetricsStr.Metrics_Metric_Aqi.Local(), Glyphs.Weather, MetricUnit.Index, MetricNeed.Place, [AirHourly(h => h.EuropeanAqi)]),
        new("usAqi", MetricSubject.Air, MetricsStr.Metrics_Metric_UsAqi.Local(), Glyphs.Weather, MetricUnit.Index, MetricNeed.Place, [AirHourly(h => h.UsAqi)]),
        new("pm25", MetricSubject.Air, MetricsStr.Metrics_Metric_Pm25.Local(), Glyphs.Fog, MetricUnit.Micrograms, MetricNeed.Place, [AirHourly(h => h.Pm25)]) { Digits = 1 },
        new("pm10", MetricSubject.Air, MetricsStr.Metrics_Metric_Pm10.Local(), Glyphs.Fog, MetricUnit.Micrograms, MetricNeed.Place, [AirHourly(h => h.Pm10)]) { Digits = 1 },
        new("ozone", MetricSubject.Air, MetricsStr.Metrics_Metric_Ozone.Local(), Glyphs.Globe, MetricUnit.Micrograms, MetricNeed.Place, [AirHourly(h => h.Ozone)]),
        new("no2", MetricSubject.Air, MetricsStr.Metrics_Metric_No2.Local(), Glyphs.Fog, MetricUnit.Micrograms, MetricNeed.Place, [AirHourly(h => h.NitrogenDioxide)]),
        Pollen("pollen", MetricsStr.Metrics_Metric_Pollen.Local(), null),
        Pollen("pollenAlder", MetricsStr.Metrics_Metric_PollenAlder.Local(), PollenType.Alder),
        Pollen("pollenBirch", MetricsStr.Metrics_Metric_PollenBirch.Local(), PollenType.Birch),
        Pollen("pollenGrass", MetricsStr.Metrics_Metric_PollenGrass.Local(), PollenType.Grass),
        Pollen("pollenMugwort", MetricsStr.Metrics_Metric_PollenMugwort.Local(), PollenType.Mugwort),
        Pollen("pollenOlive", MetricsStr.Metrics_Metric_PollenOlive.Local(), PollenType.Olive),
        Pollen("pollenRagweed", MetricsStr.Metrics_Metric_PollenRagweed.Local(), PollenType.Ragweed),

        // ---- money: a quote's bars, the ECB's days
        new("stock", MetricSubject.Money, MetricsStr.Metrics_Metric_Stock.Local(), Glyphs.Stocks, MetricUnit.Price, MetricNeed.Symbol,
            [new(MetricSource.Quote, MetricStep.Day, MetricShape.Candle, QuoteSpans) { Graph = MetricGraphType.Candles }]) { FromZero = false, Digits = 2 },
        new("exchangeRate", MetricSubject.Money, MetricsStr.Metrics_Metric_ExchangeRate.Local(), Glyphs.CurrencyExchange, MetricUnit.Ratio, MetricNeed.CurrencyPair,
            [new(MetricSource.Rates, MetricStep.Day, MetricShape.One, RateSpans) { Graph = MetricGraphType.Line }]) { FromZero = false },
    ];

    private static readonly Dictionary<string, MetricDef> ById = All.ToDictionary(m => m.Id, StringComparer.Ordinal);

    /// <summary>The metric of <paramref name="id"/>, or null for one the catalog does not have.</summary>
    public static MetricDef? Find(string? id) => id is not null && ById.TryGetValue(id, out var def) ? def : null;

    /// <summary>The metric of <paramref name="id"/>, the default for one the catalog does not have.</summary>
    public static MetricDef Of(string? id) => Find(id) ?? ById[DefaultId];

    /// <summary>[S-16304] The graph types a shape draws, the most telling first: a candle's candles or its close, a pair's lines or range.</summary>
    public static IReadOnlyList<MetricGraphType> Graphs(MetricShape shape) => shape switch
    {
        MetricShape.Candle => [MetricGraphType.Candles, MetricGraphType.Line, MetricGraphType.Area],
        MetricShape.Pair => [MetricGraphType.Line, MetricGraphType.Area, MetricGraphType.Range, MetricGraphType.Bars],
        _ => [MetricGraphType.Area, MetricGraphType.Line, MetricGraphType.Bars, MetricGraphType.Step],
    };

    /// <summary>[S-16304] A feed's graph by default: its own where it states one its shape offers, else the shape's first.</summary>
    public static MetricGraphType DefaultGraph(MetricFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        var offered = Graphs(feed.Shape);
        return feed.Graph is { } own && offered.Contains(own) ? own : offered[0];
    }

    /// <summary>[S-16302] The steps a metric has, in its feeds' order (temperature: Hour, Day).</summary>
    public static IReadOnlyList<MetricStep> Steps(MetricDef metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        return [.. metric.Feeds.Select(f => f.Step).Distinct()];
    }

    /// <summary>[S-16302] The ways a metric's step looks: history, forecast or both.</summary>
    public static IReadOnlyList<MetricWay> Ways(MetricFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        return [.. feed.Spans.Select(s => s.Way).Distinct().Order()];
    }

    /// <summary>[S-16302] The spans a feed offers one way, in its order.</summary>
    public static IReadOnlyList<MetricSpan> Spans(MetricFeed feed, MetricWay way)
    {
        ArgumentNullException.ThrowIfNull(feed);
        return [.. feed.Spans.Where(s => s.Way == way)];
    }
}
