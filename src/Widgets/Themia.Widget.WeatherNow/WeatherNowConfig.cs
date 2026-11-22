using System.Text.Json.Serialization;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-15020] What every part of the weather summary's family remembers: the place (a weather-family config). Each asks for
/// the same reading of its place, the background sync keeping it fresh ([S-2608]); what it shows is its kind.
/// </summary>
public abstract class WeatherNowPartConfig : WeatherConfig, IInterests
{
    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (WeatherInterests.For(this, interests.Unit, UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Of<WeatherInterest>().Add(wants);
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = Fresh();
        CopyTo(copy);
        return copy;
    }

    /// <summary>A new config of this kind.</summary>
    protected abstract WeatherNowPartConfig Fresh();
}

/// <summary>[S-15021] The sky now as one icon, on the icon base.</summary>
public sealed class WeatherSkyConfig : WeatherNowPartConfig
{
    protected override WeatherNowPartConfig Fresh() => new WeatherSkyConfig();
}

/// <summary>[S-15023] The condition now in words ("Partly cloudy"), on the text base: a line that sits where its card aligns it ([S-6581]).</summary>
public sealed class WeatherConditionConfig : WeatherNowPartConfig, IContentAligned
{
    protected override WeatherNowPartConfig Fresh() => new WeatherConditionConfig();
}
