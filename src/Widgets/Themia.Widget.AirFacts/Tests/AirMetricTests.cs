using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.AirFacts.Tests;

/// <summary>[S-11135] [S-14680] The air's readings, a single metric each: their values, their trends and their menu.</summary>
public sealed class AirMetricTests
{
    private static AirReading Air(string body)
        => AirQualityWire.Parse("{\"current\":{\"time\":1760000400,\"interval\":3600" + body + "}}").Value!;

    [Fact]
    public void S11135_a_reading_is_its_value_and_its_number_and_one_not_sent_is_the_dash()
    {
        var air = Air(",\"european_aqi\":25,\"pm2_5\":7.7,\"pm10\":11.9,\"uv_index\":2.5");

        Assert.Equal(new MetricValue("12 µg/m³", 11.9), AirFactRules.Metric(AirFactKind.Pm10, air));
        Assert.StartsWith("2.5 · ", AirFactRules.Metric(AirFactKind.Uv, air).Text, StringComparison.Ordinal);
        Assert.Equal(new MetricValue("—", null), AirFactRules.Metric(AirFactKind.Ozone, air));
        Assert.Equal(new MetricValue("—", null), AirFactRules.Metric(AirFactKind.Birch, air));
    }

    [Fact]
    public void S11135_the_pollen_reading_names_the_worst_and_a_type_s_its_level()
    {
        var air = Air(",\"european_aqi\":25,\"alder_pollen\":0,\"birch_pollen\":140,\"grass_pollen\":22,\"mugwort_pollen\":0,\"olive_pollen\":0,\"ragweed_pollen\":0");

        Assert.Equal($"{AirFactRules.PollenName(PollenType.Birch)} · {AirFactRules.Level(PollenLevel.High)}", AirFactRules.Value(AirFactKind.Pollen, air));
        Assert.Equal(new MetricValue(AirFactRules.Level(PollenLevel.Moderate), 22), AirFactRules.Metric(AirFactKind.Grass, air));
        Assert.Equal(AirFactRules.Level(PollenLevel.None), AirFactRules.Value(AirFactKind.Alder, air));
    }

    [Fact]
    public void S14680_the_pollutants_draw_a_line_over_the_air_hours_uv_and_each_pollen_an_arrow_the_worst_pollen_none()
    {
        Assert.Equal(MetricHistory.Series, AirFactRules.History(AirFactKind.Pm25));
        Assert.Equal(MetricHistory.Series, AirFactRules.History(AirFactKind.Ozone));
        Assert.Equal(MetricHistory.Readings, AirFactRules.History(AirFactKind.Uv));
        Assert.Equal(MetricHistory.Readings, AirFactRules.History(AirFactKind.Ragweed));
        Assert.Equal(MetricHistory.None, AirFactRules.History(AirFactKind.Pollen));
        Assert.Equal(7.7, AirFactRules.Hourly(AirFactKind.Pm25, new AirHour(DateTimeOffset.UnixEpoch) { Pm25 = 7.7 }));
    }

    [Fact]
    public void S14680_it_asks_for_the_index_s_air_answer_and_the_air_hours_only_while_a_trend_reads_them()
    {
        var pm = new AirPm25Config { Latitude = 47.37, Longitude = 8.54 };
        Assert.Equal(new AirInterest(47.37, 8.54, OnlineCadence.Air(UpdateCadence.Current)), AirInterests.For(pm));
        Assert.Null(pm.HoursInterest(TimeSpan.FromHours(1)));

        pm.Trend = MetricTrend.Line;
        Assert.Equal(new AirHoursInterest(47.37, 8.54, TimeSpan.FromHours(1)), pm.HoursInterest(TimeSpan.FromHours(1)));
        Assert.Null(new AirUvConfig { Latitude = 47.37, Longitude = 8.54, Trend = MetricTrend.Arrow }.HoursInterest(TimeSpan.FromHours(1)));
        Assert.Null(AirInterests.For(new AirPm25Config()));
    }

    [Fact]
    public void S14680_its_menu_is_the_place_then_the_trend_and_a_copy_keeps_both()
    {
        var menu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new AirOzoneModule()).MenuLines(menu, new AirOzoneConfig());
        Assert.Contains(menu.Lines, l => l.Search is not null);
        Assert.Equal(Str.Metric_Trend.Text(), menu.Lines[^1].Text);

        var copy = Assert.IsType<AirOzoneConfig>(new AirOzoneConfig { Latitude = 1, Longitude = 2, Trend = MetricTrend.Arrow }.Clone());
        Assert.Equal((1d, MetricTrend.Arrow), (copy.Latitude!.Value, copy.Trend));
    }
}
