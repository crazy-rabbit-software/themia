using System.Text.Json.Serialization;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-12707] What every weather graph holds: the weather family's place ([S-2840]) and its window, how many hours
/// from the one under way it draws (<see cref="HourChoices"/>). The window is drawn, not asked: every graph at a
/// place shares one hourly answer ([S-12702]), so a change of window refetches nothing.
/// </summary>
public abstract class HourGraphConfig : WeatherConfig, IInterests
{
    /// <summary>The window in hours; one of <see cref="HourChoices"/>, repaired to the nearest.</summary>
    public int Hours { get; set; } = 24;

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

    /// <summary>[S-12707] A window that is no choice is held to the nearest, and says so.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (HourChoices.Contains(Hours))
        {
            return [];
        }

        var was = Hours;
        Hours = EffectiveHours;
        return [$"weather graph: {was} hours is not a choice; {Hours}"];
    }

    protected void CopyTo(HourGraphConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        base.CopyTo(other);
        other.Hours = Hours;
    }
}
