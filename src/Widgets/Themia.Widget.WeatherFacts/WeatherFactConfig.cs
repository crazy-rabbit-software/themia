using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13900] [S-13903] What one weather fact card remembers: its place (the weather family's, [S-2840]) and nothing
/// else; its fact is its kind's. It reads the same weather feed as the reading (<c>weatherNow</c>): every card at one
/// place is one request ([S-2663]).
/// </summary>
public abstract class WeatherFactConfig(WeatherFactKind fact) : WeatherConfig, IInterests
{
    /// <summary>[S-13900] The one reading its kind shows.</summary>
    [JsonIgnore]
    public WeatherFactKind Fact { get; } = fact;

    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (WeatherInterests.For(this, interests.Unit, UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Of<WeatherInterest>().Add(wants);
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = WeatherFactKinds.Of(Fact).Create();
        CopyTo(copy);
        return copy;
    }
}
