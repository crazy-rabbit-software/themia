using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5100]..[S-5161] The air quality, rain and exchange-rate widgets: the wire parsers on synthetic
/// replies (never the network), the rules, the ladders, the configs and the offline store.
/// </summary>
public sealed class OnlineWidgetsTests
{
    private const long Hour = 1_791_799_200; // a whole hour, unix seconds

    private static string AirJson(string current) =>
        "{\"latitude\":47.4,\"longitude\":8.5,\"current_units\":{\"time\":\"unixtime\"},\"current\":{" + current + "}}";

    private const string GoodAir =
        "\"time\":1791799200,\"interval\":3600,\"european_aqi\":25,\"us_aqi\":36,\"pm2_5\":7.7,\"pm10\":11.9,"
        + "\"ozone\":56.0,\"uv_index\":2.5,\"alder_pollen\":0.0,\"birch_pollen\":0.0,\"grass_pollen\":12.0,"
        + "\"mugwort_pollen\":0.2,\"olive_pollen\":0.0,\"ragweed_pollen\":6.0";

    // ---- air quality ---------------------------------------------------------------------------

    [Fact]
    public void S5102_a_good_reply_parses_every_figure()
    {
        var result = AirQualityWire.Parse(AirJson(GoodAir));

        Assert.Equal(FetchStatus.Ok, result.Status);
        var air = result.Value!;
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(Hour), air.At);
        Assert.Equal(25, air.EuropeanAqi);
        Assert.Equal(36, air.UsAqi);
        Assert.Equal(7.7, air.Pm25);
        Assert.Equal(2.5, air.Uv);
        Assert.Equal(6, air.Pollen.Count);
        Assert.Equal(12, air.Pollen[PollenType.Grass]);
        Assert.False(string.IsNullOrEmpty(air.Source));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"error\":true,\"reason\":\"Latitude must be in range\"}")]
    [InlineData("{\"latitude\":1}")]
    [InlineData("{\"current\":[]}")]
    [InlineData("{\"current\":{\"european_aqi\":20}}")]
    [InlineData("{\"current\":{\"time\":\"2026-10-12T14:00\",\"european_aqi\":20}}")]
    public void S5102_a_reply_that_is_not_the_schema_is_rejected(string json)
        => Assert.Equal(FetchStatus.BadResponse, AirQualityWire.Parse(json).Status);

    [Fact]
    public void S5102_a_block_without_either_index_is_empty_not_zero()
    {
        var result = AirQualityWire.Parse(AirJson("\"time\":1791799200,\"pm2_5\":4,\"european_aqi\":null,\"us_aqi\":\"high\""));

        Assert.Equal(FetchStatus.Empty, result.Status);
        Assert.Null(result.Value);
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

    [Theory]
    [InlineData(47.37, 8.54, AirQualityScale.European)]   // Zürich
    [InlineData(64.15, -21.94, AirQualityScale.European)] // Reykjavík
    [InlineData(40.71, -74.0, AirQualityScale.Us)]        // New York
    [InlineData(35.68, 139.69, AirQualityScale.Us)]       // Tokyo
    [InlineData(-33.87, 151.21, AirQualityScale.Us)]      // Sydney
    public void S5103_auto_picks_the_european_index_in_europe(double lat, double lon, AirQualityScale expected)
        => Assert.Equal(expected, AirQualityRules.Resolve(AirQualityScale.Auto, lat, lon));

    [Fact]
    public void S5103_a_chosen_scale_wins_and_auto_without_a_place_is_us()
    {
        Assert.Equal(AirQualityScale.Us, AirQualityRules.Resolve(AirQualityScale.Us, 47.37, 8.54));
        Assert.Equal(AirQualityScale.European, AirQualityRules.Resolve(AirQualityScale.European, 40.7, -74));
        Assert.Equal(AirQualityScale.Us, AirQualityRules.Resolve(AirQualityScale.Auto, null, null));
    }

    [Theory]
    [InlineData(AirQualityScale.European, 0, AirBand.Best)]
    [InlineData(AirQualityScale.European, 20, AirBand.Best)]
    [InlineData(AirQualityScale.European, 20.5, AirBand.Second)]
    [InlineData(AirQualityScale.European, 55, AirBand.Third)]
    [InlineData(AirQualityScale.European, 100, AirBand.Fifth)]
    [InlineData(AirQualityScale.European, 140, AirBand.Worst)]
    [InlineData(AirQualityScale.Us, 50, AirBand.Best)]
    [InlineData(AirQualityScale.Us, 51, AirBand.Second)]
    [InlineData(AirQualityScale.Us, 120, AirBand.Third)]
    [InlineData(AirQualityScale.Us, 300, AirBand.Fifth)]
    [InlineData(AirQualityScale.Us, 420, AirBand.Worst)]
    public void S5104_bands_follow_each_agencys_breakpoints(AirQualityScale scale, double value, AirBand expected)
        => Assert.Equal(expected, AirQualityRules.Band(scale, value));

    [Fact]
    public void S5104_tones_are_two_good_two_caution_two_critical()
    {
        Assert.Equal(AirTone.Good, AirQualityRules.Tone(AirBand.Second));
        Assert.Equal(AirTone.Caution, AirQualityRules.Tone(AirBand.Third));
        Assert.Equal(AirTone.Caution, AirQualityRules.Tone(AirBand.Fourth));
        Assert.Equal(AirTone.Critical, AirQualityRules.Tone(AirBand.Fifth));
    }

    [Theory]
    [InlineData(0, UvLevel.Low)]
    [InlineData(2.9, UvLevel.Low)]
    [InlineData(3, UvLevel.Moderate)]
    [InlineData(6, UvLevel.High)]
    [InlineData(8, UvLevel.VeryHigh)]
    [InlineData(11, UvLevel.Extreme)]
    public void S5105_uv_follows_the_who_categories(double index, UvLevel expected)
        => Assert.Equal(expected, AirQualityRules.Uv(index));

    [Theory]
    [InlineData(PollenType.Birch, 0.5, PollenLevel.None)]
    [InlineData(PollenType.Birch, 50, PollenLevel.Moderate)]
    [InlineData(PollenType.Birch, 100, PollenLevel.High)]
    [InlineData(PollenType.Grass, 50, PollenLevel.High)]
    [InlineData(PollenType.Ragweed, 6, PollenLevel.Moderate)]
    [InlineData(PollenType.Ragweed, 20, PollenLevel.High)]
    public void S5106_pollen_levels_per_type(PollenType type, double grains, PollenLevel expected)
        => Assert.Equal(expected, AirQualityRules.Level(type, grains));

    [Fact]
    public void S5106_the_worst_pollen_comes_first()
    {
        var air = AirQualityWire.Parse(AirJson(GoodAir)).Value!;
        var pollen = AirQualityRules.Pollen(air);

        // Grass 12 and ragweed 6 are both Moderate; the larger count breaks the tie. Mugwort 0.2 is none.
        Assert.Equal([(PollenType.Grass, PollenLevel.Moderate), (PollenType.Ragweed, PollenLevel.Moderate)], pollen);
    }

    [Theory]
    [InlineData(AirQualityScale.European, 0, 0)]
    [InlineData(AirQualityScale.European, 10, 1.0 / 12)]
    [InlineData(AirQualityScale.European, 30, 1.5 / 6)]
    [InlineData(AirQualityScale.Us, 125, 2.5 / 6)]
    [InlineData(AirQualityScale.Us, 9999, 1)]
    public void S5109_the_strip_places_the_index_inside_its_band(AirQualityScale scale, double value, double expected)
        => Assert.Equal(expected, AirQualityRules.ScalePosition(scale, value), 6);

    [Fact]
    public void S5109_the_strip_is_the_rung_after_every_reading()
    {
        var floor = FluentType.Subtitle.LineHeight + FluentType.Body.LineHeight;

        Assert.False(OnlineLayouts.Air(new WidgetArea(320, 240), floor, 7).Strip);
        Assert.True(OnlineLayouts.Air(new WidgetArea(560, 420), floor, 7).Strip);
        Assert.True(OnlineLayouts.Air(new WidgetArea(800, 560), floor, 7).Strip);
    }

    // ---- rain ----------------------------------------------------------------------------------

    private static string RainJson(long start, double[] amounts, double[]? chances = null)
    {
        var times = string.Join(',', amounts.Select((_, i) => (start + (i * 900)).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var values = string.Join(',', amounts.Select(a => a.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var hourly = chances is null ? string.Empty
            : ",\"hourly\":{\"time\":[" + string.Join(',', chances.Select((_, i) => (Hour + (i * 3600)).ToString(System.Globalization.CultureInfo.InvariantCulture)))
              + "],\"precipitation_probability\":[" + string.Join(',', chances.Select(c => c.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]}";
        return "{\"minutely_15\":{\"time\":[" + times + "],\"precipitation\":[" + values + "]}" + hourly + "}";
    }

    private static RainNowcast Nowcast(params double[] amounts)
        => RainWire.Parse(RainJson(Hour + 900, amounts, [40, 70, 20])).Value!;

    /// <summary>Seven minutes into the hour: the first step (ending at :15) is the quarter under way.</summary>
    private static readonly DateTimeOffset RainNow = DateTimeOffset.FromUnixTimeSeconds(Hour + 420);

    [Fact]
    public void S5122_a_good_reply_parses_in_time_order_with_its_chances()
    {
        var cast = RainWire.Parse(RainJson(Hour, [0, 0.2, 1.4], [10, 60])).Value!;

        Assert.Equal(3, cast.Steps.Count);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(Hour + 900), cast.Steps[1].End);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(Hour), cast.Steps[1].Start);
        Assert.Equal(2, cast.Chances.Count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"minutely_15\":{\"time\":[1]}}")]
    [InlineData("{\"minutely_15\":{\"time\":1,\"precipitation\":[0]}}")]
    [InlineData("{\"error\":true,\"reason\":\"Parameter minutely_15 is invalid\"}")]
    public void S5122_a_reply_without_the_series_is_rejected(string json)
        => Assert.Equal(FetchStatus.BadResponse, RainWire.Parse(json).Status);

    [Fact]
    public void S5122_bad_steps_are_dropped_and_none_left_is_empty()
    {
        var mixed = RainWire.Parse("{\"minutely_15\":{\"time\":[1791800100,\"x\",1791801900,1791802800],\"precipitation\":[0.1,0.2,null,-3]}}");
        Assert.Single(mixed.Value!.Steps);

        var none = RainWire.Parse("{\"minutely_15\":{\"time\":[1791800100],\"precipitation\":[null]}}");
        Assert.Equal(FetchStatus.Empty, none.Status);
    }

    [Fact]
    public void S5122_the_chances_are_an_extra()
    {
        var json = "{\"minutely_15\":{\"time\":[1791800100],\"precipitation\":[0]},\"hourly\":{\"time\":\"bad\"}}";
        var cast = RainWire.Parse(json);

        Assert.Equal(FetchStatus.Ok, cast.Status);
        Assert.Empty(cast.Value!.Chances);
    }

    [Fact]
    public void S5124_dry_window_says_so_with_the_hours_chance()
    {
        var outlook = RainRules.Outlook(Nowcast(0, 0, 0, 0, 0, 0, 0, 0, 0), RainNow);

        Assert.Equal(RainHeadline.Dry, outlook.Headline);
        Assert.Equal(RainRules.WindowSteps, outlook.Steps.Count);
        Assert.Equal(40, outlook.Chance);
    }

    [Fact]
    public void S5124_rain_starting_later_counts_minutes_to_its_step_start()
    {
        // The first wet step ends at :45, so it starts at :30: 23 minutes from now, rounded to 30.
        var outlook = RainRules.Outlook(Nowcast(0, 0, 0.6, 0.8, 0, 0, 0, 0), RainNow);

        Assert.Equal(RainHeadline.StartsIn, outlook.Headline);
        Assert.Equal(30, outlook.Minutes);
        Assert.Equal(1.4, outlook.Total);
    }

    [Fact]
    public void S5124_a_change_in_the_next_quarter_is_never_zero_minutes()
    {
        var outlook = RainRules.Outlook(Nowcast(0, 0.4, 0, 0, 0, 0, 0, 0), RainNow);

        Assert.Equal(RainHeadline.StartsIn, outlook.Headline);
        Assert.Equal(15, outlook.Minutes);
    }

    [Fact]
    public void S5124_rain_now_stops_or_continues()
    {
        Assert.Equal(RainHeadline.StopsIn, RainRules.Outlook(Nowcast(1, 1, 0, 0, 0, 0, 0, 0), RainNow).Headline);
        Assert.Equal(RainHeadline.Continues, RainRules.Outlook(Nowcast(1, 1, 1, 1, 1, 1, 1, 1), RainNow).Headline);
    }

    [Fact]
    public void S5123_a_trace_under_a_tenth_is_not_rain()
    {
        Assert.Equal(RainHeadline.Dry, RainRules.Outlook(Nowcast(0.05, 0.05, 0, 0, 0, 0, 0, 0), RainNow).Headline);
        Assert.Equal(RainIntensity.None, RainRules.Intensity(0.05));
        Assert.Equal(RainIntensity.Light, RainRules.Intensity(0.5));
        Assert.Equal(RainIntensity.Moderate, RainRules.Intensity(1));
        Assert.Equal(RainIntensity.Heavy, RainRules.Intensity(2));
    }

    [Fact]
    public void S5124_a_nowcast_kept_too_long_is_outdated_not_dry()
    {
        var later = RainNow.AddMinutes(90);
        var outlook = RainRules.Outlook(Nowcast(0, 0, 0, 0, 0, 0, 0, 0), later);

        Assert.Equal(RainHeadline.Outdated, outlook.Headline);
    }

    [Fact]
    public void S5125_bars_are_on_a_fixed_scale_and_a_wet_step_is_never_invisible()
    {
        Assert.Equal(0, RainRules.BarShare(0.05));
        Assert.Equal(0.08, RainRules.BarShare(0.1));
        Assert.Equal(1, RainRules.BarShare(5));
        Assert.True(RainRules.BarShare(0.5) < RainRules.BarShare(1));
    }

    // ---- exchange rates ------------------------------------------------------------------------

    private const string Ecb = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
          <gesmes:subject>Reference rates</gesmes:subject>
          <Cube>
            <Cube time="2026-10-09"><Cube currency="USD" rate="1.12"/><Cube currency="CHF" rate="0.93"/><Cube currency="GBP" rate="0.84"/></Cube>
            <Cube time="2026-10-08"><Cube currency="USD" rate="1.10"/><Cube currency="CHF" rate="0.94"/><Cube currency="GBP" rate="0.85"/></Cube>
            <Cube time="2026-10-07"><Cube currency="USD" rate="1.08"/><Cube currency="CHF" rate="0.95"/></Cube>
          </Cube>
        </gesmes:Envelope>
        """;

    [Fact]
    public void S5141_the_ecb_document_parses_newest_day_first()
    {
        var table = EcbWire.Parse(Ecb).Value!;

        Assert.Equal(3, table.Days.Count);
        Assert.Equal(new DateOnly(2026, 10, 9), table.Latest.Date);
        Assert.Equal(1.12, table.Latest.PerEuro["USD"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"rates\":{}}")]
    [InlineData("<html><body>Service unavailable</body></html>")]
    [InlineData("<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY a \"b\">]><x>&a;</x>")]
    [InlineData("<Envelope><Cube><Cube time=\"2026-10-09\"><Cube currency=\"USD\" rate=\"1.1\"/>")]
    public void S5141_a_document_that_is_not_the_ecbs_is_rejected(string xml)
        => Assert.Equal(FetchStatus.BadResponse, EcbWire.Parse(xml).Status);

    [Fact]
    public void S5141_bad_rates_and_dates_are_dropped_and_nothing_left_is_empty()
    {
        const string Odd = """
            <Envelope><Cube>
              <Cube time="2026-10-09"><Cube currency="USD" rate="-1"/><Cube currency="usd" rate="1"/><Cube currency="CHF" rate="0.93"/><Cube currency="GBP" rate="NaN"/></Cube>
              <Cube time="yesterday"><Cube currency="USD" rate="1.1"/></Cube>
            </Cube></Envelope>
            """;

        var table = EcbWire.Parse(Odd).Value!;
        Assert.Single(table.Days);
        Assert.Equal(["CHF"], table.Latest.PerEuro.Keys);

        Assert.Equal(FetchStatus.Empty, EcbWire.Parse("<Envelope><Cube><Cube time=\"2026-10-09\"><Cube currency=\"USD\" rate=\"0\"/></Cube></Cube></Envelope>").Status);
    }

    [Fact]
    public void S5145_cross_rates_go_through_the_euro_with_the_day_change()
    {
        var table = EcbWire.Parse(Ecb).Value!;
        var chfUsd = ExchangeRules.Cross(table, "CHF", "USD")!;

        Assert.Equal(1.12 / 0.93, chfUsd.Rate, 10);
        Assert.Equal((((1.12 / 0.93) - (1.10 / 0.94)) / (1.10 / 0.94)) * 100, chfUsd.ChangePercent!.Value, 10);
        Assert.Equal(3, chfUsd.History.Count);
        Assert.Equal(1.08 / 0.95, chfUsd.History[0], 10);

        var chfEur = ExchangeRules.Cross(table, "CHF", "EUR")!;
        Assert.Equal(1 / 0.93, chfEur.Rate, 10);

        // GBP is missing on the oldest day: the history holds the two days with both.
        Assert.Equal(2, ExchangeRules.Cross(table, "CHF", "GBP")!.History.Count);
    }

    [Fact]
    public void S5145_a_currency_missing_from_the_latest_day_is_no_rate()
    {
        var table = EcbWire.Parse(Ecb).Value!;

        Assert.Null(ExchangeRules.Cross(table, "CHF", "JPY"));
        Assert.Null(ExchangeRules.Cross(table, "SEK", "USD"));
    }

    [Theory]
    [InlineData(0.93131, "0.9313")]
    [InlineData(12.34567, "12.346")]
    [InlineData(177.341, "177.34")]
    [InlineData(20036.94, "20,036.94")]
    public void S5145_rates_carry_the_precision_their_size_needs(double rate, string expected)
        => Assert.Equal(expected, ExchangeRules.Format(Locale.En, rate));

    [Theory]
    [InlineData("CH", "CHF")]
    [InlineData("li", "CHF")]
    [InlineData("DE", "EUR")]
    [InlineData("US", "USD")]
    [InlineData("GB", "GBP")]
    [InlineData("", "EUR")]
    [InlineData("AR", "EUR")]
    public void S5143_the_region_picks_the_base(string country, string expected)
        => Assert.Equal(expected, RegionCurrency.For(country));

    [Fact]
    public void S5143_an_empty_base_follows_the_region_and_the_quote_defaults_to_the_euro_else_the_dollar()
    {
        var before = RegionCurrency.Country;
        try
        {
            RegionCurrency.Country = "CH";
            var config = new ExchangeRatesConfig();

            Assert.Equal("CHF", config.EffectiveBase);
            Assert.Equal("EUR", config.EffectiveQuote);

            // A euro base: the euro is no quote of itself, so the dollar.
            RegionCurrency.Country = "DE";
            Assert.Equal("EUR", config.EffectiveBase);
            Assert.Equal("USD", config.EffectiveQuote);

            config.QuoteCurrency = "GBP";
            Assert.Equal("GBP", config.EffectiveQuote);

            // The base is never its own quote: the default for that base instead.
            config.BaseCurrency = "GBP";
            Assert.Equal("EUR", config.EffectiveQuote);
        }
        finally
        {
            RegionCurrency.Country = before;
        }
    }

    [Theory]
    [InlineData("CHF", "EUR")]
    [InlineData("USD", "EUR")]
    [InlineData("EUR", "USD")]
    public void S5143_the_default_quote(string basis, string quote) => Assert.Equal(quote, ExchangeRules.DefaultQuote(basis));

    [Fact]
    public void S5144_repair_drops_unpublished_codes_loudly()
    {
        var config = new ExchangeRatesConfig { BaseCurrency = "xyz", QuoteCurrency = "RUB" };
        var repairs = config.Repair();

        Assert.Equal(string.Empty, config.BaseCurrency);
        Assert.Equal(string.Empty, config.QuoteCurrency);
        Assert.Equal(2, repairs.Count);

        var cased = new ExchangeRatesConfig { BaseCurrency = " chf ", QuoteCurrency = "usd" };
        Assert.Empty(cased.Repair());
        Assert.Equal(("CHF", "USD"), (cased.BaseCurrency, cased.QuoteCurrency));

        Assert.Empty(new ExchangeRatesConfig().Repair());
    }

    [Fact]
    public void S5144_a_pair_round_trips_through_the_file()
    {
        var config = (ExchangeRatesConfig)ConfigJson.Read("exchangeRates", """
            { "kind": "exchangeRates", "baseCurrency": "CHF", "quoteCurrency": "JPY", "style": "chart" }
            """);

        Assert.Equal(("CHF", "JPY", ExchangeRatesStyle.Chart), (config.BaseCurrency, config.QuoteCurrency, config.Style));
        Assert.Equal("JPY", config.EffectiveQuote);
    }

    [Fact]
    public void S5103_repair_puts_an_unknown_scale_back_on_auto()
    {
        var config = new AirQualityConfig { Scale = (AirQualityScale)9 };

        Assert.Single(config.Repair());
        Assert.Equal(AirQualityScale.Auto, config.Scale);
    }

    // ---- sync and cadence ----------------------------------------------------------------------

    [Fact]
    public void S5101_each_kind_asks_its_own_source_and_never_the_weather()
    {
        var view = ViewState.Create("Online");
        view.Widgets.Add(WidgetInstance.Create(AirQualityConfig.Descriptor.Id, new Geometry.FracRect(0, 0, 0.2, 0.2)));
        view.Widgets.Add(WidgetInstance.Create(RainNextHourConfig.Descriptor.Id, new Geometry.FracRect(0.3, 0, 0.2, 0.2)));
        view.Widgets.Add(WidgetInstance.Create(ExchangeRatesConfig.Descriptor.Id, new Geometry.FracRect(0.6, 0, 0.2, 0.2)));
        view.Widgets.Add(WidgetInstance.Create(ExchangeRatesConfig.Descriptor.Id, new Geometry.FracRect(0.6, 0.5, 0.2, 0.2)));

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
        Assert.Null(SyncInterests.For((WeatherConfig)view.Widgets[0].Config));
    }

    [Fact]
    public void S5101_air_is_never_asked_faster_than_hourly_and_rain_follows_the_weather()
    {
        var refresh = new RefreshPreferences();
        UpdateCadence.Set(refresh, HostCadences.Weather, 15);

        Assert.Equal(TimeSpan.FromHours(1), OnlineCadence.Air(refresh));
        Assert.Equal(TimeSpan.FromMinutes(15), OnlineCadence.Rain(refresh));

        UpdateCadence.Set(refresh, HostCadences.Weather, 120);
        Assert.Equal(TimeSpan.FromHours(2), OnlineCadence.Air(refresh));
        Assert.Equal(TimeSpan.FromHours(3), OnlineCadence.Rates);
    }

    [Fact]
    public void S5101_without_a_place_nothing_is_asked()
    {
        Assert.Null(OnlineCadence.For(new AirQualityConfig(), TimeSpan.FromHours(1)));
        Assert.Null(OnlineCadence.For(new RainNextHourConfig(), TimeSpan.FromHours(1)));
    }

    // ---- ladders -------------------------------------------------------------------------------

    [Fact]
    public void S5107_the_air_ladder_shows_more_as_it_grows()
    {
        var floor = FluentType.Subtitle.LineHeight + FluentType.Body.LineHeight;

        var small = OnlineLayouts.Air(new WidgetArea(160, 100), floor, 5);
        var medium = OnlineLayouts.Air(new WidgetArea(320, 260), floor, 5);
        var tall = OnlineLayouts.Air(new WidgetArea(200, 460), floor, 5);
        var strip = OnlineLayouts.Air(new WidgetArea(720, 140), floor, 5);

        Assert.Equal(AirFacts.None, small.Facts);
        Assert.Equal(AirFacts.Cells, medium.Facts);
        Assert.True(medium.Count >= 3);
        Assert.Equal(AirFacts.Lines, tall.Facts);
        Assert.Equal(5, tall.Count);
        Assert.Equal(AirFacts.Side, strip.Facts);
        Assert.True(strip.Count >= 4);
        Assert.True(medium.HeroRoom >= floor);
    }

    [Fact]
    public void S5126_the_rain_ladder_adds_the_caption_then_the_bars()
    {
        var floor = FluentType.Subtitle.LineHeight;

        var tiny = OnlineLayouts.Rain(new WidgetArea(160, 60), floor, true);
        var small = OnlineLayouts.Rain(new WidgetArea(200, 80), floor, true);
        var medium = OnlineLayouts.Rain(new WidgetArea(320, 200), floor, true);

        Assert.Equal(0, tiny.ChartHeight);
        Assert.True(small.Caption);
        Assert.Equal(0, small.ChartHeight);
        Assert.True(medium.ChartHeight >= OnlineLayouts.ChartMin + FluentType.Caption.LineHeight);
    }

    [Fact]
    public void S5146_the_number_style_is_the_rate_alone_on_the_whole_tile()
    {
        var floor = FluentType.Subtitle.LineHeight + FluentType.Caption.LineHeight;

        foreach (var area in new[] { new WidgetArea(160, 120), new WidgetArea(440, 116), new WidgetArea(420, 420) })
        {
            var plan = OnlineLayouts.Rates(area, ExchangeRatesStyle.Number, floor);
            Assert.Equal(area.Height - (2 * OnlineLayouts.VerticalInset), plan.HeroRoom);
            Assert.Equal(0, plan.ChartHeight);
            Assert.False(plan.Beside);
        }
    }

    // ---- offline store -------------------------------------------------------------------------

    [Fact]
    public void S5161_stored_replies_come_back_through_the_parsers()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(Hour + 1800);
        var json = OnlineCacheStore.Serialize(
        [
            new CachedReply(OnlineSource.Air, "air|47.37,8.54", now.AddMinutes(-20), AirJson(GoodAir)),
            new CachedReply(OnlineSource.Rates, "ecb|90d", now.AddHours(-2), Ecb),
            new CachedReply(OnlineSource.Rain, "rain|47.37,8.54", now.AddMinutes(-5), "not json"),
        ]);

        var back = OnlineCacheStore.Parse(json, now)!;

        Assert.Single(back.Air);
        Assert.Equal(25, back.Air[0].Value.EuropeanAqi);
        Assert.Single(back.Rates);
        Assert.Empty(back.Rain);
    }

    [Fact]
    public void S5161_old_entries_and_foreign_documents_are_not_restored()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(Hour);
        var json = OnlineCacheStore.Serialize([new CachedReply(OnlineSource.Air, "air|1,2", now.AddDays(-3), AirJson(GoodAir))]);

        Assert.Empty(OnlineCacheStore.Parse(json, now)!.Air);
        Assert.Null(OnlineCacheStore.Parse("{\"schemaVersion\":2,\"entries\":[]}", now));
        Assert.Null(OnlineCacheStore.Parse("not json", now));
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
        Assert.Equal(RainNextHourConfig.Descriptor.Id, new RainNextHourConfig().Kind);
    }
}
