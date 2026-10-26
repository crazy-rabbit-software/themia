using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-44] The online widgets' settings as users have them, generated once with the build from before
/// they became projects (`011bda01`; Fixtures/state-online-configured.json, ids w00.., not anyone's
/// real file): a place's coordinates and name, the location-name switch, every style, the air index,
/// the pair, the instrument with its holding and colours, and the shared Fahrenheit scale. Each widget
/// now reads it from its own module's JSON context; this pins that the file still loads with no repair,
/// reads the same values and writes back byte for byte.
/// </summary>
public sealed class OnlineStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-online-configured.json"));

    [Fact]
    public void S44_the_online_widgets_configured_state_loads_without_a_repair_and_writes_back_unchanged()
    {
        var json = Fixture();
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var written = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the online widgets' state does not write back as it was read");
    }

    [Fact]
    public void S44_the_online_widgets_configured_state_reads_the_places_and_choices_it_holds()
    {
        var state = AppStateStore.Parse(Fixture()).State!;
        var configs = Assert.Single(state.Views).Widgets.Select(w => w.Config).ToList();

        var now = Assert.IsType<WeatherNowConfig>(configs[0]);
        Assert.Equal((47.3667, 8.55, "Zürich, Zurich, Switzerland"), (now.Latitude, now.Longitude, now.LocationName));
        Assert.Equal((WeatherNowStyle.LargeIcon, false), (now.Style, now.ShowDetails));

        var forecast = Assert.IsType<WeatherForecastConfig>(configs[1]);
        Assert.Equal((-33.8688, 151.2093, 14, false, ForecastStyle.Curve), (forecast.Latitude, forecast.Longitude, forecast.Days, forecast.ShowPrecipitation, forecast.Style));

        var air = Assert.IsType<AirQualityConfig>(configs[2]);
        Assert.Equal((40.7128, -74.006, AirQualityScale.Us, AirQualityStyle.Gauge), (air.Latitude, air.Longitude, air.Scale, air.Style));

        var rain = Assert.IsType<RainNextHourConfig>(configs[3]);
        Assert.Equal((52.52, 13.405, "Berlin, Land Berlin, Germany"), (rain.Latitude, rain.Longitude, rain.LocationName));

        var rates = Assert.IsType<ExchangeRatesConfig>(configs[4]);
        Assert.Equal(("CHF", "JPY", ExchangeRatesStyle.Number), (rates.BaseCurrency, rates.QuoteCurrency, rates.Style));

        var stocks = Assert.IsType<StocksConfig>(configs[5]);
        Assert.Equal(("bitcoin", QuoteMarket.Crypto, QuoteRange.Month, StocksStyle.Number), (stocks.Symbol, stocks.Market, stocks.Range, stocks.Style));
        Assert.Equal((0.25, "#22AA44", true, true), (stocks.SharesOwned, stocks.UpColor, stocks.ShowMinMax, stocks.ShowExtendedHours));

        Assert.Equal(TemperatureScale.Fahrenheit, state.Preferences.Region.Temperature);
    }
}
