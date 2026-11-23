using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.Metrics;

/// <summary>
/// [S-16305] What one Metric widget remembers: the metric (an id of <see cref="MetricCatalog"/>), its step, which way and how
/// far the graph looks, the graph's type and the style; and whatever the metric reads from (a place, a drive, an adapter, a
/// host, a GPU, a symbol, a currency pair), each kept while another metric is shown. A config holds a shared setting only
/// while its metric reads it ([S-16340], <see cref="IOffersShared"/>), so a group's page sets a place on its weather metrics
/// alone. Every property is stored; copied in <see cref="Clone"/>.
/// </summary>
public sealed class MetricsConfig : WeatherConfig, IStyled, IInterests, IReadsSystem, IOffersShared, ISetupRule, IDiskScope, IHoldsAdapter, IPingHost, IGpuAdapter
{
    private static readonly StyleOption[] MetricStyles =
    [
        StyleOption.Of(MetricsStyle.Graph, MetricsStr.Styles_Metrics_Graph.Local(), 0xE9D2),
        StyleOption.Of(MetricsStyle.Value, MetricsStr.Styles_Metrics_Value.Local(), 0xE8EF),
    ];

    /// <summary>[S-16301] The metric's id.</summary>
    public string Metric { get; set; } = MetricCatalog.DefaultId;

    /// <summary>[S-16302] Its step (where it has two: a temperature hour by hour or by day).</summary>
    public MetricStep Step { get; set; } = MetricStep.Live;

    /// <summary>[S-16302] History or forecast.</summary>
    public MetricWay Way { get; set; } = MetricWay.Past;

    /// <summary>[S-16302] How many steps the graph spans (the hub's in seconds).</summary>
    public int Span { get; set; } = 300;

    /// <summary>[S-16304] The graph's type; one the metric's shape does not draw is its default, loudly.</summary>
    public MetricGraphType Graph { get; set; } = MetricGraphType.Area;

    /// <summary>[S-16320] The style.</summary>
    public MetricsStyle Style { get; set; } = MetricsStyle.Graph;

    /// <summary>[S-16306] A drive's activity: one drive (empty: the system drive) or every drive together.</summary>
    public DiskScope Scope { get; set; }

    public string Drive { get; set; } = string.Empty;

    /// <summary>[S-16306] The network's adapter, null for Automatic, and its name as last seen.</summary>
    public string? Adapter { get; set; }

    public string? AdapterName { get; set; }

    /// <summary>[S-16306] The ping's host, empty for the default resolver.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>[S-16306] The GPU's adapter, by its position.</summary>
    public int GpuAdapter { get; set; }

    /// <summary>[S-16306] The stock's symbol, its service and the name the search gave it.</summary>
    public string Symbol { get; set; } = string.Empty;

    public QuoteMarket Market { get; set; } = QuoteMarket.Listed;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>[S-16306] The exchange rate's base and quote, empty to follow the region and its default.</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    public string QuoteCurrency { get; set; } = string.Empty;

    /// <summary>The metric as the catalog has it (the default for an unknown id).</summary>
    [JsonIgnore]
    public MetricDef Def => MetricCatalog.Of(Metric);

    /// <summary>The feed of its step.</summary>
    [JsonIgnore]
    public MetricFeed Feed => Def.Feed(Step);

    /// <summary>[S-16302] The span in force: the stored one where the feed offers it, else the feed's first.</summary>
    [JsonIgnore]
    public MetricSpan SpanInForce
    {
        get
        {
            var feed = Feed;
            return feed.Spans.FirstOrDefault(s => s.Way == Way && s.Count == Span) is { Count: > 0 } held ? held : feed.Spans[0];
        }
    }

    /// <summary>[S-16304] The graph in force: the stored one where the shape draws it, else the feed's default.</summary>
    [JsonIgnore]
    public MetricGraphType GraphInForce => MetricCatalog.Graphs(Feed.Shape).Contains(Graph) ? Graph : MetricCatalog.DefaultGraph(Feed);

    IReadOnlyList<StyleOption> IStyled.Styles => MetricStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (MetricsStyle)value;
    }

    int IGpuAdapter.Adapter
    {
        get => GpuAdapter;
        set => GpuAdapter = value;
    }

    /// <summary>[S-16305] Show <paramref name="id"/>: its first step, its first span and its graph, so a new metric starts as it reads best.</summary>
    public void Choose(string id)
    {
        if (MetricCatalog.Find(id) is not { } def || id == Metric)
        {
            return;
        }

        Metric = id;
        ChooseStep(def.Feeds[0].Step);
    }

    /// <summary>[S-16302] Read the metric at <paramref name="step"/>: that feed's first span and its default graph.</summary>
    public void ChooseStep(MetricStep step)
    {
        Step = step;
        var feed = Feed;
        (Way, Span) = (feed.Spans[0].Way, feed.Spans[0].Count);
        Graph = MetricCatalog.DefaultGraph(feed);
    }

    /// <summary>[S-16302] Look the other way: that way's first span.</summary>
    public void ChooseWay(MetricWay way)
    {
        if (MetricCatalog.Spans(Feed, way) is { Count: > 0 } spans)
        {
            (Way, Span) = (way, spans[0].Count);
        }
    }

    /// <summary>[S-16340] It holds a shared setting only while its metric reads it.</summary>
    public bool Offers(string key)
    {
        var need = Def.Need;
        return (key == SharedSettings.Place.Key && need == MetricNeed.Place)
            || (key == SharedSettings.DiskScope.Key && need == MetricNeed.Drive)
            || (key == SharedSettings.NetworkAdapter.Key && need == MetricNeed.NetworkAdapter)
            || (key == SharedSettings.PingHost.Key && need == MetricNeed.PingHost)
            || (key == SharedSettings.GpuAdapter.Key && need == MetricNeed.GpuAdapter);
    }

    /// <summary>[S-16306] It waits for setup only for what its metric needs and has not: a place, a symbol.</summary>
    bool ISetupRule.NeedsSetup(FeatureSet signedIn) => Def.Need switch
    {
        MetricNeed.Place => !HasLocation && !UseDeviceLocation,
        MetricNeed.Symbol => Quote().EffectiveSymbol.Length == 0,
        _ => false,
    };

    /// <summary>[S-1862] What its Set up line says: the place's or the symbol's sentence.</summary>
    string? ISetupRule.SetupSentence => Def.Need == MetricNeed.Symbol ? Str.Stocks_Setup.Text() : Str.Shell_Setup_Place.Text();

    /// <summary>[S-16307] The hub series it charts (only a system metric): its source at the instance its setting names.</summary>
    public SystemInterests SystemWants(string systemRoot)
    {
        if (Feed.Hub is not { } source)
        {
            return SystemInterests.None;
        }

        return source switch
        {
            SystemSource.DiskActivity => SystemInterests.Chart(SystemSource.DiskActivity, DiskScopes.Apart(DiskScopes.Instance(this, systemRoot))),
            SystemSource.Network => SystemInterests.Chart(SystemSource.Network, NetworkAdapterRules.Normalise(Adapter) ?? string.Empty),
            SystemSource.Ping => PingHosts.Wants(this),
            SystemSource.Gpu => GpuInterests.Of(GpuAdapter),
            _ => SystemInterests.Chart(source),
        };
    }

    /// <summary>[S-16307] The hub series it watches: the one <see cref="SystemWants"/> names.</summary>
    public SeriesInterest? Series(string systemRoot) => SystemWants(systemRoot).Series is { Count: > 0 } series ? series[0] : null;

    /// <summary>[S-16307] What the background sync keeps fresh for it: only what its metric reads.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        switch (Feed.Source)
        {
            case MetricSource.Hours when HoursInterest(TickInterval) is { } hours:
                interests.Of<HoursInterest>().Add(hours);
                break;
            case MetricSource.Days when DaysInterest(interests.Unit, TickInterval) is { } days:
                interests.Of<WeatherInterest>().Add(days);
                break;
            case MetricSource.AirHours when AirInterest(OnlineCadence.Air(interests.Refresh)) is { } air:
                interests.Of<AirHoursInterest>().Add(air);
                break;
            case MetricSource.Quote when QuoteInterest(UpdateCadence.Interval(interests.Refresh, HostCadences.Markets)) is { } quote:
                interests.Of<QuoteInterest>().Add(quote);
                break;
            case MetricSource.Rates:
                interests.Of<RatesInterest>().Add(new RatesInterest(OnlineCadence.Rates));
                break;
        }
    }

    /// <summary>[S-16307] The place's hours (every weather metric at the place shares one answer, asked in °C).</summary>
    public HoursInterest? HoursInterest(TimeSpan interval)
        => Feed.Source == MetricSource.Hours && WebInput.AreCoordinates(Latitude, Longitude) ? new HoursInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    /// <summary>[S-16307] The place's fourteen days (the forecast's whole: every span of the days is one answer).</summary>
    public WeatherInterest? DaysInterest(TemperatureUnit unit, TimeSpan interval)
        => Feed.Source == MetricSource.Days && WebInput.AreCoordinates(Latitude, Longitude)
            ? new WeatherInterest(Latitude!.Value, Longitude!.Value, unit, WeatherReport.ForecastDays, interval)
            : null;

    /// <summary>[S-16307] The place's air hours.</summary>
    public AirHoursInterest? AirInterest(TimeSpan interval)
        => Feed.Source == MetricSource.AirHours && WebInput.AreCoordinates(Latitude, Longitude) ? new AirHoursInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    /// <summary>[S-16307] The quote over the span's range; null without a usable symbol.</summary>
    public QuoteInterest? QuoteInterest(TimeSpan interval)
        => Feed.Source == MetricSource.Quote ? Quote().Interest(interval) : null;

    /// <summary>[S-16306] The stock's settings as the price widgets hold them, its range the span's: what the symbol's line and the quote's want read.</summary>
    public StockQuoteConfig Quote() => new MetricsQuote
    {
        Symbol = Symbol,
        Market = Market,
        DisplayName = DisplayName,
        Range = MetricAdapters.Range(SpanInForce),
    };

    /// <summary>[S-16306] Take the stock's settings back from <paramref name="quote"/> (its symbol's line wrote them).</summary>
    public void Take(StockQuoteConfig quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        (Symbol, Market, DisplayName) = (quote.Symbol, quote.Market, quote.DisplayName);
    }

    /// <summary>[S-16306] The pair as the exchange widgets hold it.</summary>
    public ExchangePairConfig Pair() => new MetricsPair { BaseCurrency = BaseCurrency, QuoteCurrency = QuoteCurrency };

    /// <summary>[S-16306] Take the pair back from <paramref name="pair"/>.</summary>
    public void Take(ExchangePairConfig pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        (BaseCurrency, QuoteCurrency) = (pair.BaseCurrency, pair.QuoteCurrency);
    }

    /// <summary>
    /// [S-16305] An unknown metric is the default, a step it has not its first, a span or a graph it does not offer its own
    /// first, each loudly; what its sources read is repaired as theirs are (a drive that is no letter, an adapter of none).
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        if (MetricCatalog.Find(Metric) is null)
        {
            notes.Add($"metrics: '{Clip(Metric)}' is no metric; showing {MetricCatalog.DefaultId}");
            Metric = MetricCatalog.DefaultId;
            ChooseStep(Def.Feeds[0].Step);
        }

        if (!MetricCatalog.Steps(Def).Contains(Step))
        {
            notes.Add($"metrics: {Metric} has no step {Step}; reading its first");
            ChooseStep(Def.Feeds[0].Step);
        }

        if (!Feed.Spans.Any(s => s.Way == Way && s.Count == Span))
        {
            notes.Add($"metrics: {Metric} offers no span {Way} {Span}; showing its first");
            (Way, Span) = (Feed.Spans[0].Way, Feed.Spans[0].Count);
        }

        if (!MetricCatalog.Graphs(Feed.Shape).Contains(Graph))
        {
            notes.Add($"metrics: {Metric} draws no {Graph}; drawing its default");
            Graph = MetricCatalog.DefaultGraph(Feed);
        }

        if (!Enum.IsDefined(Style))
        {
            notes.Add($"metrics: no style {(int)Style}; the graph");
            Style = MetricsStyle.Graph;
        }

        Host ??= string.Empty;
        notes.AddRange(DiskScopes.Repair(this, "metrics"));
        notes.AddRange(NetworkAdapterRules.Repair(this, "metrics"));
        notes.AddRange(GpuInterests.Repair(new WidgetId("metrics"), GpuAdapter, a => GpuAdapter = a));
        var pair = Pair();
        notes.AddRange(pair.Repair());
        Take(pair);
        return notes;
    }

    private static string Clip(string? text) => text is null ? "null" : text.Length <= 24 ? text : text[..24];

    public override WidgetConfig Clone()
    {
        var copy = new MetricsConfig
        {
            Metric = Metric,
            Step = Step,
            Way = Way,
            Span = Span,
            Graph = Graph,
            Style = Style,
            Scope = Scope,
            Drive = Drive,
            Adapter = Adapter,
            AdapterName = AdapterName,
            Host = Host,
            GpuAdapter = GpuAdapter,
            Symbol = Symbol,
            Market = Market,
            DisplayName = DisplayName,
            BaseCurrency = BaseCurrency,
            QuoteCurrency = QuoteCurrency,
        };
        CopyTo(copy);
        return copy;
    }

    /// <summary>The stock's settings in the price widgets' shape: never stored, never a widget.</summary>
    private sealed class MetricsQuote : StockQuoteConfig
    {
        public override WidgetConfig Clone() => CopyQuoteTo(new MetricsQuote());
    }

    /// <summary>The pair in the exchange widgets' shape: never stored, never a widget.</summary>
    private sealed class MetricsPair : ExchangePairConfig
    {
        public override WidgetConfig Clone() => CopyPairTo(new MetricsPair());
    }
}
