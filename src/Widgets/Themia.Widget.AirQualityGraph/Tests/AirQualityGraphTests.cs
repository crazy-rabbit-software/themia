using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.AirQualityGraph.Tests;

/// <summary>[S-10400]..[S-10404] The air graph's mapping, bands, want and config.</summary>
public sealed class AirQualityGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static AirHours Hours(params double?[] pm25)
        => new([.. pm25.Select((v, i) => new AirHour(T0.AddHours(i - 1)) { Pm25 = v, EuropeanAqi = v * 2, UsAqi = v * 4 })]);

    /// <summary>Zürich: in Europe, so Auto reads the European index.</summary>
    private static AirQualityGraphConfig Config(AirMeasure measure = AirMeasure.Index, AirQualityScale scale = AirQualityScale.Auto)
        => new() { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Switzerland", Hours = 24, Measure = measure, Scale = scale };

    private static HourGraphModel? Model(AirHours hours, AirQualityGraphConfig config)
        => AirQualityGraphRules.Model(hours, config, T0.AddMinutes(20), Locale.En, TimeZoneInfo.Utc);

    [Fact]
    public void S10403_the_pollutants_bands_are_the_european_agencys()
    {
        Assert.Equal(AirBand.Best, AirQualityGraphRules.Band(AirMeasure.Pm25, AirQualityScale.Us, 10));
        Assert.Equal(AirBand.Second, AirQualityGraphRules.Band(AirMeasure.Pm25, AirQualityScale.Us, 10.1));
        Assert.Equal(AirBand.Worst, AirQualityGraphRules.Band(AirMeasure.Pm25, AirQualityScale.European, 80));
        Assert.Equal(AirBand.Third, AirQualityGraphRules.Band(AirMeasure.Ozone, AirQualityScale.European, 120));
        Assert.Equal(AirBand.Fourth, AirQualityGraphRules.Band(AirMeasure.NitrogenDioxide, AirQualityScale.European, 200));
        Assert.Equal(AirBand.Fifth, AirQualityGraphRules.Band(AirMeasure.Pm10, AirQualityScale.European, 120));
        Assert.Equal(AirBand.Third, AirQualityGraphRules.Band(AirMeasure.Index, AirQualityScale.Us, 120));
    }

    [Fact]
    public void S10402_the_index_follows_the_scale_auto_by_the_place()
    {
        var hours = Hours(5, 10, 30);

        Assert.Equal("20 · Good", Model(hours, Config())!.Reading);
        Assert.Equal("40 · Good", Model(hours, Config(scale: AirQualityScale.Us))!.Reading);
    }

    [Fact]
    public void S10402_the_index_names_neither_the_kind_nor_the_place_its_glyph_leads_the_reading()
    {
        var model = Model(Hours(5, 10, 30), Config())!;

        Assert.Equal(string.Empty, model.Label);
        Assert.Equal(AirQualityGraphModule.Glyph, model.Glyph);
        Assert.DoesNotContain("Zürich", model.Reading, StringComparison.Ordinal);
    }

    [Fact]
    public void S10402_a_pollutant_is_read_on_its_bands_with_the_worst_hour_marked()
    {
        var model = Model(Hours(5, 8, 22, 30, 60), Config(AirMeasure.Pm25))!;

        Assert.Equal([ColourRole.Accent, ColourRole.Caution, ColourRole.Caution, ColourRole.Critical], model.Slots.Select(s => s.Role));
        Assert.Equal(new HourMark(3, "60"), model.Marks.Single());
        Assert.Equal("8 · Good", model.Reading);
        Assert.StartsWith("PM2.5", model.Label, StringComparison.Ordinal);
        Assert.Equal(ColourRole.Critical, Model(Hours(5, 60, 8), Config(AirMeasure.Pm25))!.ReadingRole);
    }

    [Fact]
    public void S10402_a_clean_window_stays_low_and_gaps_stay_gaps()
    {
        var model = Model(Hours(1, 2, null, 4), Config(AirMeasure.Pm25))!;

        Assert.Equal(0.1, model.Slots[0].Value!.Value, 3);
        Assert.Null(model.Slots[1].Value);
        Assert.Null(Model(Hours(1, 2, null, null), Config(AirMeasure.Pm25)));
    }

    [Fact]
    public void S10400_the_air_graph_asks_for_the_airs_hourly_answer_and_offers_up_to_four_days()
    {
        var builder = new InterestsBuilder(new RefreshPreferences(), TemperatureUnit.Celsius);
        Config().AddInterests(builder);

        Assert.Single(builder.Of<AirHoursInterest>());
        Assert.Empty(builder.Of<HoursInterest>());
        Assert.Equal(48, new AirQualityGraphConfig().EffectiveHours);
        Assert.Equal(96, new AirQualityGraphConfig { Hours = 90 }.EffectiveHours);
    }

    [Fact]
    public void S10400_a_bad_measure_or_scale_is_repaired_and_a_clone_keeps_them()
    {
        var config = new AirQualityGraphConfig { Measure = (AirMeasure)9, Scale = (AirQualityScale)9, Hours = 12 };

        Assert.Equal(3, config.Repair().Count);
        Assert.Equal((AirMeasure.Index, AirQualityScale.Auto, 24), (config.Measure, config.Scale, config.Hours));
        var copy = (AirQualityGraphConfig)Config(AirMeasure.Ozone, AirQualityScale.Us).Clone();
        Assert.Equal((AirMeasure.Ozone, AirQualityScale.Us), (copy.Measure, copy.Scale));
    }
}
