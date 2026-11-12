using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-11105] What one weather facts card remembers: its place (the weather family's, [S-2840]) and the
/// facts it shows, which and in what order. It reads the same weather feed as the reading
/// (<c>weatherNow</c>): two cards at one place are one request ([S-2663]).
/// </summary>
public sealed class WeatherFactsConfig : WeatherConfig, IInterests
{
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

    /// <summary>[S-11105] The facts it shows, in order: one row each; the card's height follows the count ([S-11106]).</summary>
    public List<WeatherFactKind> Facts { get; set; } = [.. WeatherFactRules.Defaults];

    public override IReadOnlyList<string> Repair()
    {
        var (facts, changed) = SkyFacts.Repair<WeatherFactKind>(Facts, WeatherFactRules.All, WeatherFactRules.Defaults);
        Facts = facts;
        return changed ? ["weather facts: the pick was empty or held a fact twice or one it does not have; repaired"] : [];
    }

    public override WidgetConfig Clone()
    {
        var copy = new WeatherFactsConfig { Facts = [.. Facts] };
        CopyTo(copy);
        return copy;
    }
}
