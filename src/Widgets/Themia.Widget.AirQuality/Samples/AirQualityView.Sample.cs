using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2052] The air quality's sample: the sample day's readings at its place and its UV, built
/// through the SDK's parser, so it is exactly what a reply would have become. Nothing is fetched or saved.
/// </summary>
internal sealed partial class AirQualityView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSample(SampleReading(poor: false), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>The sample day's reading (<paramref name="poor"/>: a bad day's), as a reply would have become.</summary>
    internal static Fetched<AirReading> SampleReading(bool poor)
    {
        var at = SampleDay.Now.ToUnixTimeSeconds() / 3600 * 3600;
        var uv = SampleWeather.UvIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var json = "{\"current\":{\"time\":" + at + ",\"interval\":3600"
            + (poor
                ? ",\"european_aqi\":72,\"us_aqi\":118,\"pm2_5\":48.6,\"pm10\":71.2,\"ozone\":131.0,\"uv_index\":7.4"
                  + ",\"alder_pollen\":0,\"birch_pollen\":140,\"grass_pollen\":22,\"mugwort_pollen\":0,\"olive_pollen\":0,\"ragweed_pollen\":0"
                : ",\"european_aqi\":25,\"us_aqi\":36,\"pm2_5\":7.7,\"pm10\":11.9,\"ozone\":41.0,\"uv_index\":" + uv
                  + ",\"alder_pollen\":0,\"birch_pollen\":0,\"grass_pollen\":12,\"mugwort_pollen\":0.2,\"olive_pollen\":0,\"ragweed_pollen\":0")
            + "}}";
        return new Fetched<AirReading>(FetchStatus.Ok, AirQualityWire.Parse(json).Value, null, SampleDay.MinutesAgo(12));
    }

    /// <summary>The sample place, and <paramref name="reading"/> held as the feed's answer.</summary>
    internal void HoldSample(Fetched<AirReading>? reading, FetchStatus status)
    {
        _config = new AirQualityConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName, Style = _config.Style, Scale = _config.Scale };
        PublishPlace();
        _feed.Preview(reading, status);
    }
}
