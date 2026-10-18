#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): fixture answers for the three online widgets, so their ladders can
/// be looked at without a place, the network or the sync. Built through the Core parsers, so a
/// fixture is exactly what a reply would have become. Nothing is fetched or saved.
/// </summary>
internal sealed partial class AirQualityView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "poor"];

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
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}

internal sealed partial class ExchangeRatesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state != "sample")
        {
            return Task.CompletedTask;
        }

        // Ninety days of a gently wandering euro, as the ECB document would carry them.
        var day = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var xml = new System.Text.StringBuilder("<Envelope><Cube>");
        for (int i = 0, written = 0; written < 63; i++)
        {
            var date = day.AddDays(-i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var t = 63 - written++;
            string Rate(double centre, double swing, double drift)
                => (centre + (Math.Sin(t / 7.0) * swing) + (drift * t)).ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

            xml.Append("<Cube time=\"").Append(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append("\">")
                .Append("<Cube currency=\"USD\" rate=\"").Append(Rate(1.12, 0.012, 0.0002)).Append("\"/>")
                .Append("<Cube currency=\"CHF\" rate=\"").Append(Rate(0.932, 0.004, 0.00005)).Append("\"/>")
                .Append("<Cube currency=\"GBP\" rate=\"").Append(Rate(0.846, 0.006, -0.0001)).Append("\"/>")
                .Append("<Cube currency=\"JPY\" rate=\"").Append(Rate(177.3, 1.8, 0.02)).Append("\"/>")
                .Append("<Cube currency=\"SEK\" rate=\"").Append(Rate(11.16, 0.08, 0.001)).Append("\"/>")
                .Append("</Cube>");
        }

        xml.Append("</Cube></Envelope>");

        _config = new ExchangeRatesConfig { BaseCurrency = "CHF", QuoteCurrency = "EUR", Style = _config.Style };
        _table = new Fetched<RateTable>(FetchStatus.Ok, EcbWire.Parse(xml.ToString()).Value, null, DateTimeOffset.Now.AddHours(-2));
        _web = null;
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
