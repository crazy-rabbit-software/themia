using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13900] [S-13903] [S-14603] What one weather fact card remembers: its place (the weather family's, [S-2840]) and its
/// trend; its fact is its kind's. It reads the same weather feed as the sky and the condition (<c>weatherSky</c>, <c>weatherCondition</c>): every card at one
/// place is one request ([S-2663]); a trend over the service's hours asks the place's hours too (the weather graphs'
/// one answer), only while it draws one.
/// </summary>
public abstract class WeatherFactConfig(WeatherFactKind fact) : WeatherConfig, IInterests, IMetricGraph
{
    /// <summary>[S-13900] The one reading its kind shows.</summary>
    [JsonIgnore]
    public WeatherFactKind Fact { get; } = fact;

    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MetricTrend Trend { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public MetricHistory History => WeatherFactKinds.Of(Fact).History;

    /// <inheritdoc />
    [JsonIgnore]
    public MetricGraph? Graph => WeatherFactKinds.Of(Fact).Graph;

    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Span { get; set; }

    /// <summary>[S-14703] How its graph over the hours is drawn: its kind's graph's default unless another is stored.</summary>
    [JsonIgnore]
    public ChartType Chart
    {
        get => ChartStored ?? DefaultChart;
        set => ChartStored = value == DefaultChart ? null : value;
    }

    /// <summary>[S-14710] The chart type stored: only one other than its kind's default.</summary>
    [JsonPropertyName("chart")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChartType? ChartStored { get; set; }

    /// <summary>[S-14703] Simple by default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ChartDetail Detail { get; set; }

    /// <summary>[S-14710] The types its kind's graph offers; none without a graph.</summary>
    [JsonIgnore]
    public IReadOnlyList<ChartType> ChartTypes => MetricGraphs.Types(Graph);

    [JsonIgnore]
    public ChartType DefaultChart => MetricGraphs.Default(Graph);

    [JsonIgnore]
    public bool OffersDetail => MetricGraphs.OffersDetail(Graph);

    /// <summary>[S-14603] Whether it reads the place's hours: a trend over them is drawn.</summary>
    [JsonIgnore]
    public bool ReadsHours => History == MetricHistory.Series && MetricRules.Effective(this) != MetricTrend.None;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        var interval = UpdateCadence.Interval(interests.Refresh, HostCadences.Weather);
        if (WeatherInterests.For(this, interests.Unit, interval) is { } wants)
        {
            interests.Of<WeatherInterest>().Add(wants);
        }

        if (HoursInterest(interval) is { } hours)
        {
            interests.Of<HoursInterest>().Add(hours);
        }
    }

    /// <summary>[S-14603] The place's hours it asks for at <paramref name="interval"/>: null without a place or a trend over them.</summary>
    public HoursInterest? HoursInterest(TimeSpan interval)
        => ReadsHours && WebInput.AreCoordinates(Latitude, Longitude) ? new HoursInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    /// <summary>[S-14603] A trend its fact cannot draw is the one it can, loudly.</summary>
    public override IReadOnlyList<string> Repair()
        => [.. base.Repair(), .. MetricRules.Repair(this) is { } note ? [note] : Array.Empty<string>(), .. ChartLooks.Repair(this, GetType().Name),
            .. MetricGraphs.RepairSpan(this, GetType().Name) is { } span ? [span] : Array.Empty<string>()];

    public override WidgetConfig Clone()
    {
        var copy = (WeatherFactConfig)WeatherFactKinds.Of(Fact).Create();
        CopyTo(copy);
        MetricGraphs.Copy(this, copy);
        return copy;
    }
}
