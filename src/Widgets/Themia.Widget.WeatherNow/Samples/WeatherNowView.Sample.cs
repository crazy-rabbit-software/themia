using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] [S-2052] The reading's sample: the sample day's reading at its place, without a place,
/// the network or the sync service. Nothing is fetched.
/// </summary>
internal sealed partial class WeatherNowView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Preview(SamplePlaceReading(), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>The card's style at the sample place; the sample day's reading there.</summary>
    private Fetched<CurrentWeather> SamplePlaceReading()
    {
        _config = new WeatherNowConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName, Style = _config.Style };
        PublishPlace();
        return new Fetched<CurrentWeather>(FetchStatus.Ok, SampleWeather.Reading(), null, SampleDay.MinutesAgo(4));
    }
}
