#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): the sample day's reading at its place, so the ladder can
/// be looked at without a place, the network or the sync service. Nothing is fetched.
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

        _config = new WeatherNowConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName, Style = _config.Style };
        _applied = (WeatherNowConfig)_config.Clone();
        _want = null;
        var reading = new Fetched<CurrentWeather>(FetchStatus.Ok, SampleWeather.Reading(), null, SampleDay.MinutesAgo(4));

        // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
        _reading = state switch
        {
            "stale" => reading.Stale(FetchStatus.Offline, null),
            "failed" => null,
            _ => reading,
        };
        _status = state == "failed" ? FetchStatus.Unreachable : FetchStatus.Ok;
        PublishPlace();
        Render();
        return Task.CompletedTask;
    }
}
#endif
