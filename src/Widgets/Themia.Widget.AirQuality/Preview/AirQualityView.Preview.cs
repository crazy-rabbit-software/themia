#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): fixture readings (<c>sample</c>, <c>poor</c>), so the ladder can be
/// looked at without a place, the network or the sync. Built through the SDK's parser, so a fixture
/// is exactly what a reply would have become. Nothing is fetched or saved.
/// </summary>
internal sealed partial class AirQualityView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "poor", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "poor"))
        {
            return Task.CompletedTask;
        }

        var at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 3600 * 3600;
        var poor = state == "poor";
        var json = "{\"current\":{\"time\":" + at + ",\"interval\":3600"
            + (poor
                ? ",\"european_aqi\":72,\"us_aqi\":118,\"pm2_5\":48.6,\"pm10\":71.2,\"ozone\":131.0,\"uv_index\":7.4"
                  + ",\"alder_pollen\":0,\"birch_pollen\":140,\"grass_pollen\":22,\"mugwort_pollen\":0,\"olive_pollen\":0,\"ragweed_pollen\":0"
                : ",\"european_aqi\":25,\"us_aqi\":36,\"pm2_5\":7.7,\"pm10\":11.9,\"ozone\":56.0,\"uv_index\":2.5"
                  + ",\"alder_pollen\":0,\"birch_pollen\":0,\"grass_pollen\":12,\"mugwort_pollen\":0.2,\"olive_pollen\":0,\"ragweed_pollen\":0")
            + "}}";

        _config = new AirQualityConfig { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Zurich, Switzerland", Style = _config.Style };
        _applied = (AirQualityConfig)_config.Clone();
        _want = null;
        _reading = new Fetched<AirReading>(FetchStatus.Ok, AirQualityWire.Parse(json).Value, null, DateTimeOffset.Now.AddMinutes(-12));
        if (state == "stale")
        {
            // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
            _reading = _reading.Stale(FetchStatus.Offline, null);
        }
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
