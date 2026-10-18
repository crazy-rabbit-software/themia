using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6160] The styles of the weather, forecast, stock, exchange-rate and air widgets side by side:
/// their menus and their clones. Moved from Core's WeatherStylesTests into the module test host, which
/// has all five once they are projects; unchanged in what they assert.
/// </summary>
public sealed class OnlineStylesTests
{
    private static string[] Names(WidgetId kind) => [.. WidgetStyles.For(kind).Select(s => s.Name)];

    [Fact]
    public void S6160_each_kind_offers_its_styles_in_menu_order_the_default_first()
    {
        Assert.Equal(["standard", "largeIcon", "minimal"], Names(new WidgetId("weatherNow")));
        Assert.Equal(["rows", "columns", "curve", "grid"], Names(new WidgetId("weatherForecast")));
        Assert.Equal(["chart", "number"], Names(new WidgetId("stocks")));
        Assert.Equal(["chart", "number"], Names(new WidgetId("exchangeRates")));
        Assert.Equal(["number", "gauge"], Names(new WidgetId("airQuality")));

        Assert.Equal(WeatherNowStyle.Standard, new WeatherNowConfig().Style);
        Assert.Equal(ForecastStyle.Rows, new WeatherForecastConfig().Style);
        Assert.Equal(StocksStyle.Chart, new StocksConfig().Style);
        Assert.Equal(ExchangeRatesStyle.Chart, new ExchangeRatesConfig().Style);
        Assert.Equal(AirQualityStyle.Number, new AirQualityConfig().Style);
    }

    [Fact]
    public void S6160_every_style_survives_a_clone()
    {
        Assert.Equal(WeatherNowStyle.Minimal, ((WeatherNowConfig)new WeatherNowConfig { Style = WeatherNowStyle.Minimal }.Clone()).Style);
        Assert.Equal(StocksStyle.Number, ((StocksConfig)new StocksConfig { Style = StocksStyle.Number }.Clone()).Style);
        Assert.Equal(AirQualityStyle.Gauge, ((AirQualityConfig)new AirQualityConfig { Style = AirQualityStyle.Gauge }.Clone()).Style);
    }
}
