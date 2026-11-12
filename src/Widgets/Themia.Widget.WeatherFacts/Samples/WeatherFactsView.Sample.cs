using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-11850] [S-1917] [S-2052] The facts' sample: the sample day's reading at the sample place (the same the
/// reading card's sample draws, so a recipe's atoms agree), nothing fetched.
/// </summary>
internal sealed partial class WeatherFactsView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _feed.Preview(SamplePlaceReading(), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>The card's facts at the sample place; the sample day's reading there.</summary>
    private Fetched<CurrentWeather> SamplePlaceReading()
    {
        _config = new WeatherFactsConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName, Facts = [.. _config.Facts] };
        _header.SetAbout(_config.PlaceLabel);
        return new Fetched<CurrentWeather>(FetchStatus.Ok, SampleWeather.Reading(), null, SampleDay.MinutesAgo(4));
    }
}
