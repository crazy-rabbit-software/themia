#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.AirFacts;

/// <summary>
/// Debug renders only ([S-1917]): the air index's fixture readings (<c>sample</c>: the sample day's at the
/// sample place, its UV the weather's; <c>poor</c>), through the SDK's parser, so the two atoms of a recipe
/// agree. <c>stale</c> is the sample through a failed refresh, <c>failed</c> no answer at all.
/// </summary>
internal sealed partial class AirFactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "poor", "stale", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "poor" or "stale" or "failed"))
        {
            return Task.CompletedTask;
        }

        var at = SampleDay.Now.ToUnixTimeSeconds() / 3600 * 3600;
        var uv = SampleWeather.UvIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var json = "{\"current\":{\"time\":" + at + ",\"interval\":3600"
            + (state == "poor"
                ? ",\"european_aqi\":72,\"us_aqi\":118,\"pm2_5\":48.6,\"pm10\":71.2,\"ozone\":131.0,\"uv_index\":7.4"
                  + ",\"alder_pollen\":0,\"birch_pollen\":140,\"grass_pollen\":22,\"mugwort_pollen\":0,\"olive_pollen\":0,\"ragweed_pollen\":0"
                : ",\"european_aqi\":25,\"us_aqi\":36,\"pm2_5\":7.7,\"pm10\":11.9,\"ozone\":41.0,\"uv_index\":" + uv
                  + ",\"alder_pollen\":0,\"birch_pollen\":0,\"grass_pollen\":12,\"mugwort_pollen\":0.2,\"olive_pollen\":0,\"ragweed_pollen\":0")
            + "}}";
        _config = new AirFactsConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName, Facts = [.. _config.Facts] };
        _header.SetAbout(_config.PlaceLabel);
        var reading = new Fetched<AirReading>(FetchStatus.Ok, AirQualityWire.Parse(json).Value, null, SampleDay.MinutesAgo(12));
        _feed.Preview(
            state switch
            {
                "stale" => reading.Stale(FetchStatus.Offline, null),
                "failed" => null,
                _ => reading,
            },
            state == "failed" ? FetchStatus.Unreachable : FetchStatus.Ok);
        return Task.CompletedTask;
    }
}
#endif
