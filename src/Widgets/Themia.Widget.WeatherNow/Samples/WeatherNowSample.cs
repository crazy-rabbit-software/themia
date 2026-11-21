using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.WeatherNow;

/// <summary>
/// [S-11850] [S-2052] The weather summary family's sample: the sample day's reading at the sample place, without a place,
/// the network or the sync service; the sky, the condition and the temperature of a group agree. Nothing is fetched.
/// </summary>
internal static class WeatherNowSample
{
    /// <summary><paramref name="config"/> at the sample place.</summary>
    public static T Placed<T>(T config)
        where T : WeatherNowPartConfig
    {
        config.Latitude = SamplePlace.Latitude;
        config.Longitude = SamplePlace.Longitude;
        config.LocationName = SamplePlace.LocationName;
        return config;
    }

    /// <summary>The sample day's reading, kept through a failed refresh when <paramref name="stale"/> ([S-7042]).</summary>
    public static Fetched<CurrentWeather> Answer(bool stale = false)
    {
        var reading = new Fetched<CurrentWeather>(FetchStatus.Ok, SampleWeather.Reading(), null, SampleDay.MinutesAgo(4));
        return stale ? reading.Stale(FetchStatus.Offline, null) : reading;
    }
}

/// <summary>[S-11850] The sky on the sample day's reading.</summary>
internal sealed partial class WeatherSkyView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Preview(WeatherNowSample.Placed(new WeatherSkyConfig()), WeatherNowSample.Answer(), FetchStatus.Ok);
        return Task.CompletedTask;
    }
}

/// <summary>[S-11850] The condition on the sample day's reading.</summary>
internal sealed partial class WeatherConditionView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Preview(WeatherNowSample.Placed(new WeatherConditionConfig()), WeatherNowSample.Answer(), FetchStatus.Ok);
        return Task.CompletedTask;
    }
}
