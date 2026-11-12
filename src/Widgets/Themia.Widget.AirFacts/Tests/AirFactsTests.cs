using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.AirFacts.Tests;

/// <summary>[S-11135]..[S-11137] The air facts: its readings, its pick, its sizing and its menu.</summary>
public sealed class AirFactsTests
{
    private static readonly WidgetDescriptor Kind = new AirFactsModule().Descriptor;

    private static AirReading Air(string body)
        => AirQualityWire.Parse("{\"current\":{\"time\":1760000400,\"interval\":3600" + body + "}}").Value!;

    [Fact]
    public void S11135_a_new_card_shows_uv_pm25_pollen_and_pm10()
        => Assert.Equal([AirFactKind.Uv, AirFactKind.Pm25, AirFactKind.Pollen, AirFactKind.Pm10], new AirFactsConfig().Facts);

    [Fact]
    public void S11135_each_picked_row_is_one_row_and_a_reading_not_sent_is_the_dash()
    {
        var air = Air(",\"european_aqi\":25,\"pm2_5\":7.7,\"pm10\":11.9,\"uv_index\":2.5");

        var facts = AirFactRules.For([AirFactKind.Pm10, AirFactKind.Uv, AirFactKind.Pollen, AirFactKind.Birch, AirFactKind.Ozone], air);

        Assert.Equal([AirFactKind.Pm10, AirFactKind.Uv, AirFactKind.Pollen, AirFactKind.Birch, AirFactKind.Ozone], facts.Select(f => f.Kind));
        Assert.Equal("12 µg/m³", facts[0].Value);
        Assert.StartsWith("2.5 · ", facts[1].Value, StringComparison.Ordinal);
        Assert.Equal(["—", "—", "—"], facts.Skip(2).Select(f => f.Value));
    }

    [Fact]
    public void S11135_the_pollen_row_names_the_worst_and_a_type_s_row_its_level()
    {
        var air = Air(",\"european_aqi\":25,\"alder_pollen\":0,\"birch_pollen\":140,\"grass_pollen\":22,\"mugwort_pollen\":0,\"olive_pollen\":0,\"ragweed_pollen\":0");

        Assert.Equal($"{AirFactRules.PollenName(PollenType.Birch)} · {AirFactRules.Level(PollenLevel.High)}", AirFactRules.Value(AirFactKind.Pollen, air));
        Assert.Equal(AirFactRules.Level(PollenLevel.Moderate), AirFactRules.Value(AirFactKind.Grass, air));
        Assert.Equal(AirFactRules.Level(PollenLevel.None), AirFactRules.Value(AirFactKind.Alder, air));
    }

    [Fact]
    public void S11136_its_height_is_a_28_row_a_reading_under_the_header_and_dragging_sets_the_rows()
    {
        var config = new AirFactsConfig();
        Assert.Equal(new SizeDip(240, 36 + (4 * 28) + 12), Kind.Sizing(config).Natural);

        var shorter = (AirFactsConfig)Kind.SizeSets!(config, new SizeDip(240, 36 + (2 * 28) + 12));
        Assert.Equal([AirFactKind.Uv, AirFactKind.Pm25], shorter.Facts);
        Assert.Equal(4, config.Facts.Count);
    }

    [Fact]
    public void S11135_an_empty_pick_is_repaired_and_a_copy_keeps_its_own()
    {
        var config = new AirFactsConfig { Facts = [] };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(AirFactRules.Defaults, config.Facts);
        Assert.NotSame(config.Facts, ((AirFactsConfig)config.Clone()).Facts);
    }

    [Fact]
    public void S11137_the_menu_is_the_rows_then_the_place()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        new AirFactsModule().MenuLines(menu, new AirFactsConfig());

        Assert.Equal(AirFactsStr.AirFacts_Menu_Facts.Text(), menu.Lines[0].Text);
        Assert.Contains(menu.Lines, l => l.Search is not null);
    }

    [Fact]
    public void S11135_it_asks_for_the_same_air_answer_as_the_index_at_one_place()
    {
        var facts = new AirFactsConfig { Latitude = 47.37, Longitude = 8.54 };
        Assert.Equal(new AirInterest(47.37, 8.54, OnlineCadence.Air(UpdateCadence.Current)), AirInterests.For(facts));
        Assert.Null(AirInterests.For(new AirFactsConfig()));
    }
}
