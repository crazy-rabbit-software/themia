using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] [S-2052] The forecast's sample: the sample day's week at its place, in the card's own
/// style ([S-6006]), without a place, the network or the sync service. Nothing is fetched or saved.
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSampleWeek();
        PublishPlace();
        Render();
        return Task.CompletedTask;
    }

    /// <summary>[S-2448] The card's own config (its style, its days: what its size was drawn for) at the sample place, holding the sample week.</summary>
    private void HoldSampleWeek()
    {
        var sample = (WeatherForecastConfig)_config.Clone();
        sample.Latitude = SamplePlace.Latitude;
        sample.Longitude = SamplePlace.Longitude;
        sample.LocationName = SamplePlace.LocationName;
        _config = sample;

        _applied = (WeatherForecastConfig)_config.Clone();
        _want = null;
        _forecast = new Fetched<Forecast>(FetchStatus.Ok, new Forecast(SampleWeather.Days(_config.EffectiveDays)), null, SampleDay.MinutesAgo(4));
    }
}
