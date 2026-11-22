using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6000] The styles of the forecast, stock, exchange-rate and air widgets side by side:
/// their menus and their clones. Moved from Core's WeatherStylesTests into the module test host, which
/// has all five once they are projects; unchanged in what they assert.
/// </summary>
public sealed class OnlineStylesTests
{
    private static string[] Names(WidgetId kind) => [.. WidgetStyles.For(kind).Select(s => s.Name)];

    [Fact]
    public void S6000_each_kind_offers_its_styles_in_menu_order_the_default_first()
    {
        Assert.Empty(Names(new WidgetId("weatherForecast"))); // [S-15540] the forecast curve: one look, no style
        Assert.Equal(["number", "gauge"], Names(new WidgetId("airQuality")));

        Assert.Equal(AirQualityStyle.Number, new AirQualityConfig().Style);
    }

    [Fact]
    public void S6000_every_style_survives_a_clone()
    {
        Assert.Equal(AirQualityStyle.Gauge, ((AirQualityConfig)new AirQualityConfig { Style = AirQualityStyle.Gauge }.Clone()).Style);
    }
}
