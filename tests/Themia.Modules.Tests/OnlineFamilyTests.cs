using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Xunit;
using static Themia.Core.Tests.OnlineReplies;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5100] [S-5101] [S-5102] The air, rain and exchange-rate widgets side by side: each asks its own source and
/// never the weather, and their configs round-trip; the air reply's figures as the air widget's rules read them. Moved from Core's OnlineWidgetsTests into the module
/// test host, which has all three once they are projects; unchanged in what they assert.
/// </summary>
public sealed class OnlineFamilyTests
{
    [Fact]
    public void S5101_each_kind_asks_its_own_source_and_never_the_weather()
    {
        var view = ViewState.Create("Online");
        view.Widgets.Add(WidgetInstance.Create(new Sdk.WidgetId("airQuality"), new Geometry.FracRect(0, 0, 0.2, 0.2)));
        view.Widgets.Add(WidgetInstance.Create(new Sdk.WidgetId("rainNextHour"), new Geometry.FracRect(0.3, 0, 0.2, 0.2)));
        view.Widgets.Add(WidgetInstance.Create(new Sdk.WidgetId("exchangeRates"), new Geometry.FracRect(0.6, 0, 0.2, 0.2)));
        view.Widgets.Add(WidgetInstance.Create(new Sdk.WidgetId("exchangeRates"), new Geometry.FracRect(0.6, 0.5, 0.2, 0.2)));

        foreach (var w in view.Widgets)
        {
            if (w.Config is WeatherConfig place)
            {
                place.Latitude = 47.37;
                place.Longitude = 8.54;
            }
        }

        var state = new AppState();
        state.Views.Clear();
        state.Views.Add(view);
        var interests = SyncInterests.From(state);

        Assert.Empty(interests.Weather);
        Assert.Single(interests.Air);
        Assert.Single(interests.Rain);
        Assert.Equal(new RatesInterest(OnlineCadence.Rates), interests.Rates);
        Assert.Contains("aq|", interests.Key());
        Assert.Contains("fx|", interests.Key());
        Assert.Null(WeatherInterests.For((WeatherConfig)view.Widgets[0].Config));
    }

    [Fact]
    public void S5101_without_a_place_nothing_is_asked()
    {
        Assert.Null(new AirQualityConfig().Interest(TimeSpan.FromHours(1)));
        Assert.Null(new RainNextHourConfig().Interest(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void S5100_the_configs_round_trip_and_clone()
    {
        var air = new AirQualityConfig { Latitude = 1, Longitude = 2, LocationName = "X", Scale = AirQualityScale.Us };
        var rates = new ExchangeRatesConfig { BaseCurrency = "CHF", QuoteCurrency = "USD", Style = ExchangeRatesStyle.Chart };

        var airCopy = (AirQualityConfig)air.Clone();
        var ratesCopy = (ExchangeRatesConfig)rates.Clone();
        rates.QuoteCurrency = "GBP";

        Assert.Equal(AirQualityScale.Us, airCopy.Scale);
        Assert.Equal(2, airCopy.Longitude);
        Assert.Equal(("CHF", "USD"), (ratesCopy.BaseCurrency, ratesCopy.QuoteCurrency));
        Assert.Equal(ExchangeRatesStyle.Chart, ratesCopy.Style);
        Assert.Equal(new Sdk.WidgetId("rainNextHour"), new RainNextHourConfig().Kind);
    }

    [Fact]
    public void S5102_out_of_range_and_null_figures_are_dropped_one_by_one()
    {
        var air = AirQualityWire.Parse(AirJson(
            "\"time\":1791799200,\"european_aqi\":30,\"us_aqi\":-4,\"uv_index\":99,\"pm2_5\":null,\"grass_pollen\":null,\"birch_pollen\":3")).Value!;

        Assert.Equal(30, air.EuropeanAqi);
        Assert.Null(air.UsAqi);
        Assert.Null(air.Uv);
        Assert.Null(air.Pm25);
        Assert.Single(air.Pollen);
        Assert.True(AirQualityRules.HasPollen(air));
    }

    [Fact]
    public void S5102_no_pollen_forecast_is_told_apart_from_none_in_the_air()
    {
        var outside = AirQualityWire.Parse(AirJson("\"time\":1791799200,\"us_aqi\":54,\"grass_pollen\":null")).Value!;
        var clean = AirQualityWire.Parse(AirJson("\"time\":1791799200,\"us_aqi\":54,\"grass_pollen\":0.0")).Value!;

        Assert.False(AirQualityRules.HasPollen(outside));
        Assert.True(AirQualityRules.HasPollen(clean));
        Assert.Empty(AirQualityRules.Pollen(clean));
    }
}
