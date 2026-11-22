using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-11850] [S-1917] [S-2052] A weather fact's sample: the sample day's reading at the sample place (the same the
/// reading card's sample draws, so a recipe's atoms agree) and the sample day's hours (the weather graphs'), nothing fetched.
/// </summary>
internal sealed partial class WeatherFactView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _clock = () => SampleDay.Now;
        _feed.Preview(SamplePlaceReading(), FetchStatus.Ok);
        _hours.Preview(new Fetched<WeatherHours>(FetchStatus.Ok, SampleHours.Weather(), null, SampleDay.MinutesAgo(4)), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>The card at the sample place; the sample day's reading there.</summary>
    private Fetched<CurrentWeather> SamplePlaceReading()
    {
        var config = (WeatherFactConfig)WeatherFactKinds.Of(_fact).Create();
        config.Latitude = SamplePlace.Latitude;
        config.Longitude = SamplePlace.Longitude;
        config.LocationName = SamplePlace.LocationName;
        config.Trend = _config.Trend;
        config.Day = _config.Day;
        _config = config;
        return new Fetched<CurrentWeather>(FetchStatus.Ok, SampleWeather.Reading(), null, SampleDay.MinutesAgo(4));
    }
}
