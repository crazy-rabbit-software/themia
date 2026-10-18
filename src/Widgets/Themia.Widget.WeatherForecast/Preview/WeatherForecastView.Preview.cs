#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture week for Zürich, so each style's ladder can be looked at
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
            Latitude = 47.37,
            Longitude = 8.54,
            LocationName = "Zürich, Zurich, Switzerland",
            Style = _config.Style,
        };

        _applied = (WeatherForecastConfig)_config.Clone();
        _want = null;
        // [S-6172] The current conditions' sample reading is the temperature now.
        _nowTemperature = WeatherSample.Reading().Temperature;
        _forecast = new Fetched<Forecast>(FetchStatus.Ok, new Forecast(WeatherSample.Days(_config.EffectiveDays)), null, DateTimeOffset.Now.AddMinutes(-4));
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
