using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.Widget.AirFacts;

/// <summary>
/// [S-14680] One kind of the air facts family: one of the air's readings where the user says as a single metric
/// (<see cref="MetricModule"/>), at home in its family under Weather (<see cref="AddFolderId.AirFacts"/>). SPEC.md
/// beside this file.
/// </summary>
public abstract class AirMetricModule(string key)
    : MetricModule(AirKinds.Of(key), AirFactsJsonContext.Default, AddFolderId.AirFacts, AirKinds.Rank(key))
{
    /// <summary>[S-11137] The place's search ([S-3722]); the reading is its kind, never a setting.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, (AirMetricConfig)config));

    public override FrameworkElement CreateView(IWidgetHost host) => new AirMetricView(host, Enum.Parse<AirFactKind>(Kind.Key));
}

/// <summary>
/// [S-14680] What one air card remembers: its place (the weather family's, [S-2840]) and its trend; its reading is its
/// kind's. It asks for the same air answer as the index (<c>airQuality</c>, [S-5101]); a trend over the hours asks the
/// place's air hours too (the air graph's one answer), only while it draws one.
/// </summary>
public abstract class AirMetricConfig(string key) : WeatherConfig, IInterests, IMetricTrend, IChartLooks
{
    /// <summary>The reading its kind shows.</summary>
    [JsonIgnore]
    public AirFactKind Fact { get; } = Enum.Parse<AirFactKind>(key);

    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MetricTrend Trend { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public MetricHistory History => AirFactRules.History(Fact);

    /// <summary>[S-14703] How its graph over the hours is drawn; Area (the look until now) by default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ChartType Chart { get; set; }

    /// <summary>[S-14703] Simple by default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ChartDetail Detail { get; set; }

    /// <summary>[S-14703] The weather graphs' types (the line with its wash, the line alone, a column per hour) where it has a series; none without.</summary>
    [JsonIgnore]
    public IReadOnlyList<ChartType> ChartTypes => History == MetricHistory.Series ? ChartLooks.Hours : [];

    [JsonIgnore]
    public ChartType DefaultChart => ChartType.Area;

    [JsonIgnore]
    public bool OffersDetail => History == MetricHistory.Series;

    /// <summary>[S-14603] Whether it reads the place's air hours: a trend over them is drawn.</summary>
    [JsonIgnore]
    public bool ReadsHours => History == MetricHistory.Series && MetricRules.Effective(this) != MetricTrend.None;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        var interval = OnlineCadence.Air(interests.Refresh);
        if (AirInterests.For(this, interval) is { } wants)
        {
            interests.Of<AirInterest>().Add(wants);
        }

        if (HoursInterest(interval) is { } hours)
        {
            interests.Of<AirHoursInterest>().Add(hours);
        }
    }

    /// <summary>[S-14603] The place's air hours it asks for at <paramref name="interval"/>: null without a place or a trend over them.</summary>
    public AirHoursInterest? HoursInterest(TimeSpan interval)
        => ReadsHours && WebInput.AreCoordinates(Latitude, Longitude) ? new AirHoursInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    /// <summary>[S-14603] A trend its reading cannot draw is the one it can, loudly.</summary>
    public override IReadOnlyList<string> Repair()
        => [.. base.Repair(), .. MetricRules.Repair(this) is { } note ? [note] : Array.Empty<string>(), .. ChartLooks.Repair(this, GetType().Name)];

    public override WidgetConfig Clone()
    {
        var copy = (AirMetricConfig)AirKinds.Of(Fact.ToString()).Create();
        CopyTo(copy);
        copy.Trend = Trend;
        ChartLooks.Copy(this, copy);
        return copy;
    }
}

/// <summary>
/// [S-14680] One of the air's readings at the card's place on the single metric's face (<see cref="MetricView"/>), from
/// the same air answer the index draws (<see cref="SkyFeed{TWant, T}"/>); a pollutant's trend over the place's air hours
/// (this hour against the last, the hours from the last on, [S-14603]). A reading kept through a failed refresh stays and
/// the status's problem glyph says so ([S-7042]); with none, the row stays with "--" and why is its tooltip ([S-12813]).
/// </summary>
internal sealed partial class AirMetricView : MetricView
{
    private readonly AirFactKind _fact;
    private readonly SkyFeed<AirInterest, AirReading> _feed;
    private readonly SkyFeed<AirHoursInterest, AirHours> _hours;
    private AirMetricConfig _config;

    /// <summary>The moment the hour under way is found at: now, or the sample moment in a picture.</summary>
    private Func<DateTimeOffset> _clock = () => DateTimeOffset.Now;

    public AirMetricView(IWidgetHost host, AirFactKind fact)
        : base(host, AirKinds.Of(fact.ToString()))
    {
        _fact = fact;
        _config = (AirMetricConfig)AirKinds.Of(fact.ToString()).Create();
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Air, host.Weather.Show),
            () => AirInterests.For(_config),
            Render);
        _hours = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.AirHours, host.Weather.Show),
            () => _config.HoursInterest(OnlineCadence.Air(UpdateCadence.Current)),
            Render);
    }

    protected override string MetricName => AirFactRules.Name(_fact);

    /// <summary>[S-730] Refresh: the network now.</summary>
    protected override IReadOnlyList<WidgetAction> OwnActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), Refresh)] : [];

    protected override void OnAttach(Ticker ticker, WidgetConfig config)
    {
        _config = Own(config);
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        _hours.Attach(ticker, TimeSpan.FromMinutes(1));
        OnApply(config);
    }

    protected override void OnApply(WidgetConfig config)
    {
        var own = Own(config);
        if (own.Latitude != _config.Latitude || own.Longitude != _config.Longitude)
        {
            Restart();
        }

        _config = own;
        _feed.Reload();
        _hours.Reload();
    }

    protected override void OnDetach()
    {
        _feed.Detach();
        _hours.Detach();
    }

    private void Refresh()
    {
        _feed.Refresh();
        if (_config.ReadsHours)
        {
            _hours.Refresh();
        }
    }

    /// <summary>The card's config, or its kind's default for one that is not (never another reading's).</summary>
    private AirMetricConfig Own(WidgetConfig config)
        => config is AirMetricConfig own && own.Fact == _fact ? own : (AirMetricConfig)AirKinds.Of(_fact.ToString()).Create();

    private void Render()
    {
        if (_feed.Held is not { Value: { } air } held)
        {
            // [S-12813] The row stays, "--", the reason its tooltip (a status glyph would stand over the "--").
            Present(new MetricShown(MetricValue.Unread) { Why = OnlineParts.Caption(_feed.Status) });
            return;
        }

        var shown = new MetricShown(AirFactRules.Metric(_fact, air)) { Reading = held.At.UtcTicks, Problems = OnlineParts.Problems(held) };
        if (History == MetricHistory.Series && _hours.Held is { Value: { } hours })
        {
            var (direction, series) = MetricRules.Hours(hours.Hours, h => h.At, h => AirFactRules.Hourly(_fact, h), 1, _clock());
            // [S-14703] The pollutants' hours are concentrations, written as the reading is.
            shown = shown with { Direction = direction, Series = series, Figure = AirFactRules.Concentration };
        }

        Present(shown);
    }
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AirUvConfig))]
[JsonSerializable(typeof(AirPm25Config))]
[JsonSerializable(typeof(AirPollenConfig))]
[JsonSerializable(typeof(AirPm10Config))]
[JsonSerializable(typeof(AirOzoneConfig))]
[JsonSerializable(typeof(AirAlderConfig))]
[JsonSerializable(typeof(AirBirchConfig))]
[JsonSerializable(typeof(AirGrassConfig))]
[JsonSerializable(typeof(AirMugwortConfig))]
[JsonSerializable(typeof(AirOliveConfig))]
[JsonSerializable(typeof(AirRagweedConfig))]
internal sealed partial class AirFactsJsonContext : JsonSerializerContext;
