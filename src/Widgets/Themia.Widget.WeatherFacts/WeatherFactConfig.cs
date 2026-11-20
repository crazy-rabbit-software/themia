using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13900] [S-13903] [S-14603] What one weather fact card remembers: its place (the weather family's, [S-2840]) and its
/// trend; its fact is its kind's. It reads the same weather feed as the reading (<c>weatherNow</c>): every card at one
/// place is one request ([S-2663]); a trend over the service's hours asks the place's hours too (the weather graphs'
/// one answer), only while it draws one.
/// </summary>
public abstract class WeatherFactConfig(WeatherFactKind fact) : WeatherConfig, IInterests, IMetricTrend, IChartLooks
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
    public MetricHistory History => WeatherFactRules.History(Fact);

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
        => [.. base.Repair(), .. MetricRules.Repair(this) is { } note ? [note] : Array.Empty<string>(), .. ChartLooks.Repair(this, GetType().Name)];

    public override WidgetConfig Clone()
    {
        var copy = (WeatherFactConfig)WeatherFactKinds.Of(Fact).Create();
        CopyTo(copy);
        copy.Trend = Trend;
        ChartLooks.Copy(this, copy);
        return copy;
    }
}
