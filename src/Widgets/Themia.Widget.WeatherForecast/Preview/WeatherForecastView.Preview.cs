#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): the sample day's week at its place, so each style's ladder can be looked at
/// without a place, the network or the sync service. Nothing is fetched or saved. <c>sample</c>
/// keeps the card's style ([S-6006]: <c>--style rows|columns|curve|grid</c>).
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "stale"))
        {
            return Task.CompletedTask;
        }

        // [S-2448] The card's own config (its style, its days: what its size was drawn for) at the sample place.
        var sample = (WeatherForecastConfig)_config.Clone();
        sample.Latitude = SamplePlace.Latitude;
        sample.Longitude = SamplePlace.Longitude;
        sample.LocationName = SamplePlace.LocationName;
        _config = sample;

        _applied = (WeatherForecastConfig)_config.Clone();
        _want = null;
        _forecast = new Fetched<Forecast>(FetchStatus.Ok, new Forecast(SampleWeather.Days(_config.EffectiveDays)), null, SampleDay.MinutesAgo(4));
        if (state == "stale")
        {
            // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
            _forecast = _forecast.Stale(FetchStatus.Offline, null);
        }
        PublishPlace();
        Render();
        return Task.CompletedTask;
    }
}
#endif
