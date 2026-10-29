#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture reading for Zürich on a cloudy autumn afternoon, so the
/// ladder can be looked at without a place, the network or the sync service. Nothing is fetched.
/// <c>failed</c> is no reading and the service out of reach: the Status row ([S-6734]).
/// </summary>
internal sealed partial class WeatherNowView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "stale" or "failed"))
        {
            return Task.CompletedTask;
        }

        _config = new WeatherNowConfig { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Zurich, Switzerland", Style = _config.Style };
        _applied = (WeatherNowConfig)_config.Clone();
        _want = null;
        var reading = new Fetched<CurrentWeather>(FetchStatus.Ok, WeatherSample.Reading(), null, DateTimeOffset.Now.AddMinutes(-4));

        // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
        _reading = state switch
        {
            "stale" => reading.Stale(FetchStatus.Offline, null),
            "failed" => null,
            _ => reading,
        };
        _status = state == "failed" ? FetchStatus.Unreachable : FetchStatus.Ok;
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
