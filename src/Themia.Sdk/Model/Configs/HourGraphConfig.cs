using System.Text.Json.Serialization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-12707] What every weather graph holds: the weather family's place ([S-2840]) and its window, how many hours
/// from the one under way it draws (<see cref="HourChoices"/>). The window is drawn, not asked: every graph at a
/// place shares one hourly answer ([S-12702]), so a change of window refetches nothing. [S-6752] And its chart look:
/// the line with its wash, the line alone or a column per hour (the kind's own look until now the default), Simple or
/// Detailed.
/// </summary>
public abstract class HourGraphConfig : WeatherConfig, IInterests, IChartLooks
{
    /// <param name="chart">The kind's own type: what it drew before there was a choice.</param>
    protected HourGraphConfig(ChartType chart)
    {
        DefaultChart = chart;
        Chart = chart;
    }

    /// <summary>The window in hours; one of <see cref="HourChoices"/>, repaired to the nearest.</summary>
    public int Hours { get; set; } = 24;

    /// <summary>[S-6752] How the hours are drawn.</summary>
    public ChartType Chart { get; set; }

    /// <summary>[S-6752] Simple by default.</summary>
    public ChartDetail Detail { get; set; }

    [JsonIgnore]
    public IReadOnlyList<ChartType> ChartTypes => ChartLooks.Hours;

    [JsonIgnore]
    public ChartType DefaultChart { get; }

    [JsonIgnore]
    public bool OffersDetail => true;

    /// <summary>[S-6752] The look drawn: the chosen type when offered, else the kind's own.</summary>
    [JsonIgnore]
    public HourGraphLook Look => ChartLooks.Effective(this) switch
    {
        ChartType.Line => HourGraphLook.Line,
        ChartType.Bars => HourGraphLook.Bars,
        _ => HourGraphLook.Area,
    };

    /// <summary>The windows this graph offers.</summary>
    [JsonIgnore]
    public virtual IReadOnlyList<int> HourChoices => HourGraphRules.HourChoices;

    /// <summary>The window drawn: <see cref="Hours"/> when it is a choice, else the nearest choice.</summary>
    [JsonIgnore]
    public int EffectiveHours => HourChoices.Contains(Hours) ? Hours : HourChoices.MinBy(c => Math.Abs(c - Hours));

    /// <summary>[S-2608] What the background sync keeps fresh for it: the place's hourly answer.</summary>
    public virtual void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (Interest(UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Of<HoursInterest>().Add(wants);
        }
    }

    /// <summary>[S-12702] What it asks for at the running app's cadence ([S-3780]), or null without a place.</summary>
    public HoursInterest? Interest() => Interest(UpdateCadence.Interval(HostCadences.Weather));

    /// <summary>[S-12702] <see cref="Interest()"/> at a given cadence.</summary>
    public HoursInterest? Interest(TimeSpan interval)
        => WebInput.AreCoordinates(Latitude, Longitude) ? new HoursInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    /// <summary>[S-12707] A window that is no choice is held to the nearest, and says so; [S-6752] a chart look not offered too.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(ChartLooks.Repair(this, "weather graph"));
        if (!HourChoices.Contains(Hours))
        {
            var was = Hours;
            Hours = EffectiveHours;
            notes.Add($"weather graph: {was} hours is not a choice; {Hours}");
        }

        return notes;
    }

    protected void CopyTo(HourGraphConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        base.CopyTo(other);
        other.Hours = Hours;
        ChartLooks.Copy(this, other);
    }
}
