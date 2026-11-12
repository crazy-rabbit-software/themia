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

        _config = new WeatherForecastConfig
        {
            Latitude = SamplePlace.Latitude,
            Longitude = SamplePlace.Longitude,
            LocationName = SamplePlace.LocationName,
            Style = _config.Style,
        };

        _applied = (WeatherForecastConfig)_config.Clone();
        _want = null;
        _forecast = new Fetched<Forecast>(FetchStatus.Ok, new Forecast(SampleWeather.Days(_config.EffectiveDays)), null, SampleDay.MinutesAgo(4));
        if (state == "stale")
        {
            // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
            _forecast = _forecast.Stale(FetchStatus.Offline, null);
        }
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
