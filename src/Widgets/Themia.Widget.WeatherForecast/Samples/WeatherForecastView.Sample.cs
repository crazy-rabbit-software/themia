using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] [S-2052] The forecast curve's sample: the sample day's week at its place, without a place, the network or the sync service. Nothing is fetched or saved.
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSampleWeek();
        return Task.CompletedTask;
    }

    /// <summary>[S-2448] The card's own config (its precipitation line; its days are what its size reaches) at the sample place, holding the sample week.</summary>
    private void HoldSampleWeek()
    {
        var sample = (WeatherForecastConfig)_config.Clone();
        sample.Latitude = SamplePlace.Latitude;
        sample.Longitude = SamplePlace.Longitude;
        sample.LocationName = SamplePlace.LocationName;
        _config = sample;

        _feed.Preview(new Fetched<Forecast>(FetchStatus.Ok, new Forecast(SampleWeather.Days(WeatherForecastConfig.MostDays)), null, SampleDay.MinutesAgo(4)), FetchStatus.Ok);
    }
}
