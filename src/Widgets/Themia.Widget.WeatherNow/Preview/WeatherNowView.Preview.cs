#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture reading for Zürich on a cloudy autumn afternoon, so the
/// ladder can be looked at without a place, the network or the sync service. Nothing is fetched.
/// </summary>
internal sealed partial class WeatherNowView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state != "sample")
        {
            return Task.CompletedTask;
        }

        _config = new WeatherNowConfig { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Zurich, Switzerland", Style = _config.Style };
        _applied = (WeatherNowConfig)_config.Clone();
        _want = null;
        _reading = new Fetched<CurrentWeather>(FetchStatus.Ok, WeatherSample.Reading(), null, DateTimeOffset.Now.AddMinutes(-4));
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
