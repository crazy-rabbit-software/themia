using System.Text.Json.Serialization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-724] The forecast curve: the next few days' highs and lows, from open-meteo's <c>daily</c> block. One look, no style
/// ([S-15540]: its rows, columns and grid came back as ready-made repeaters).
/// <para>
/// A separate widget from the weather now (<c>weatherSky</c>, <c>weatherCondition</c>, <c>weatherTemperature</c>) rather than a mode of it, because the two
/// answer different questions and want different shapes on the desktop: one is a readout, the other
/// a chart across days.
/// </para>
/// </summary>
public sealed class WeatherForecastConfig : WeatherConfig, IInterests
{
    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <summary>[S-11145] [S-14502] It asks for every day open-meteo gives: how many it shows is its card's.</summary>
    [JsonIgnore]
    public override int? ForecastDays => MostDays;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (WeatherInterests.For(this, interests.Unit, UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Of<WeatherInterest>().Add(wants);
        }
    }

    /// <summary>
    /// [S-11145] [S-14410] [S-14502] How many days a new card shows: its natural size's. The days shown after are as many as
    /// its card holds (1 to <see cref="MostDays"/>, open-meteo's own limit), never stored.
    /// </summary>
    public const int DefaultDays = 5;

    /// <summary>[S-11145] The most days it shows and asks for: open-meteo's own limit.</summary>
    public const int MostDays = 14;

    /// <summary>Show each day's precipitation amount under its column.</summary>
    public bool ShowPrecipitation { get; set; } = true;

    public override WidgetConfig Clone()
    {
        var copy = new WeatherForecastConfig
        {
            ShowPrecipitation = ShowPrecipitation,
        };

        CopyTo(copy);
        return copy;
    }
}
