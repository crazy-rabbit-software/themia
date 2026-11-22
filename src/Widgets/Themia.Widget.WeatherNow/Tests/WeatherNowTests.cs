using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeatherNow.Tests;

/// <summary>
/// [S-15020]..[S-15024] The weather summary split in three: the sky an icon kind, the condition a text kind, the temperature
/// the weather facts' metric; each over its place's reading, opening its forecast; the placed summary they stand in for.
/// </summary>
public sealed class WeatherNowTests
{
    private static readonly IconModule Sky = new WeatherSkyModule();
    private static readonly TextModule Condition = new WeatherConditionModule();

    private static readonly CurrentWeather Overcast = new(11.4, 9, 80, 12, 3);

    [Fact]
    public void S15020_the_family_is_the_sky_and_the_condition_in_the_weather_summary_family_in_its_order()
    {
        Assert.Equal(("weatherSky", "weatherCondition"), (Sky.Descriptor.Id.Value, Condition.Descriptor.Id.Value));
        Assert.All([Sky.Descriptor, Condition.Descriptor], d => Assert.Equal(AddFolderId.WeatherSummary, d.Add.Shelf));
        Assert.Equal((0, 1), (Sky.Descriptor.Add.Rank, Condition.Descriptor.Add.Rank));
        Assert.Equal(1, Sky.Descriptor.Add.Popular);
        Assert.False(ModuleCatalog.TryGet(new WidgetId(WeatherSummary.Removed), out _));
    }

    [Fact]
    public void S15020_every_part_has_its_words_in_every_language()
    {
        foreach (var descriptor in new[] { Sky.Descriptor, Condition.Descriptor })
        {
            Assert.All(Enum.GetValues<Locale>(), locale =>
            {
                Assert.False(string.IsNullOrWhiteSpace(descriptor.Label.Text(locale)));
                Assert.False(string.IsNullOrWhiteSpace(descriptor.Describe(locale)));
            });
        }

        Assert.Equal(("Weather icon", "Condition"), (Sky.Descriptor.Label.Text(Locale.En), Condition.Descriptor.Label.Text(Locale.En)));
    }

    [Fact]
    public void S15021_the_sky_is_an_icon_kind_its_glyph_the_conditions_its_words_the_tooltip()
    {
        Assert.Equal(IconRules.Sizing().Natural, Sky.Descriptor.Natural);

        var shown = WeatherSkyView.Shown(Overcast, "Offline", ["stale"]);
        Assert.Equal(WeatherGlyph.Of(WeatherReport.Look(3)), shown.Glyph);
        Assert.Equal((TileState.Off, WeatherReport.Describe(3)), (shown.State, shown.Tooltip));
        Assert.Equal(["stale"], shown.Problems);
    }

    [Fact]
    public void S15021_without_a_reading_the_sky_stays_unavailable_and_says_why()
    {
        var shown = WeatherSkyView.Shown(null, "Set a place", []);

        Assert.Equal((Glyphs.Weather, TileState.Unavailable, "Set a place"), (shown.Glyph, shown.State, shown.Tooltip));
    }

    [Fact]
    public void S15023_the_condition_is_one_line_in_a_details_tone_with_no_lead()
    {
        var kind = ((TextModule)Condition).Kind;
        Assert.Equal((1, false, TextTone.Secondary), (kind.Lines, kind.Lead, kind.Tone));
        Assert.Equal(TextRules.Sizing(kind).Natural, Condition.Descriptor.Natural);

        Assert.Equal((WeatherReport.Describe(3), true), (WeatherConditionView.Shown(Overcast, "x", []).Text, WeatherConditionView.Shown(Overcast, "x", []).Available));
    }

    [Fact]
    public void S15023_without_a_reading_the_condition_keeps_its_face_and_says_why_beside_it_never_in_its_place()
    {
        var none = WeatherConditionView.Shown(null, "Can't reach the service", ["Can't reach the service"]);

        Assert.Equal((string.Empty, false, "Can't reach the service"), (none.Text, none.Available, none.Why));
        Assert.Equal((TextRules.NoText, false), TextRules.Drawn(none, ((TextModule)Condition).Kind.None?.Text() ?? string.Empty));
        Assert.Equal(["Can't reach the service"], none.Problems);
    }

    [Fact]
    public void S15021_a_sky_that_failed_is_the_status_marks_as_a_stale_one_is()
    {
        var failed = WeatherSkyView.Shown(null, "Can't reach the service", ["Can't reach the service"]);

        Assert.Equal((TileState.Unavailable, "Can't reach the service"), (failed.State, failed.Tooltip));
        Assert.Equal(["Can't reach the service"], failed.Problems);
    }

    [Fact]
    public void S15021_only_a_failure_is_a_problem_never_a_place_not_set_offline_or_loading()
    {
        Assert.Equal([FetchState.Message(FetchStatus.Unreachable)], OnlineParts.Failure(FetchStatus.Unreachable));
        Assert.Single(OnlineParts.Failure(FetchStatus.TimedOut));
        Assert.Empty(OnlineParts.Failure(FetchStatus.NotConfigured));
        Assert.Empty(OnlineParts.Failure(FetchStatus.Offline));
        Assert.Empty(OnlineParts.Failure(FetchStatus.Loading));
        Assert.Empty(OnlineParts.Failure(FetchStatus.Ok));
    }

    [Fact]
    public void S2035_both_parts_open_the_forecast_for_their_place_and_nothing_without_one()
    {
        var sky = new WeatherSkyConfig { Latitude = 47.3769, Longitude = 8.5417, LocationName = "Zurich, Switzerland" };
        var condition = (WeatherConditionConfig)new WeatherConditionConfig { Latitude = 47.3769, Longitude = 8.5417, LocationName = "Zurich, Switzerland" }.Clone();

        var skyOpen = Sky.Kind.Opens!.Target(sky, new IconShown(Glyphs.Weather));
        var conditionOpen = Condition.Kind.Opens!.Target(condition, new TextShown("Overcast"));

        Assert.StartsWith("https://www.msn.com/", skyOpen!.Value, StringComparison.Ordinal);
        Assert.Equal(skyOpen, conditionOpen);
        Assert.Null(Sky.Kind.Opens.Target(new WeatherSkyConfig(), new IconShown(Glyphs.Weather)));
    }

    [Fact]
    public void S15025_the_parts_declare_the_summary_they_stand_in_for()
    {
        Assert.Equal(WeatherSummary.Sky, Sky.Descriptor.Merges);
        Assert.Equal(WeatherSummary.Condition, Condition.Descriptor.Merges);
        Assert.All(WeatherSummary.Temperature, m => Assert.Contains(m, ModuleCatalog.Of(new WidgetId("weatherTemperature")).Merges));
        Assert.All([.. WeatherSummary.Sky, .. WeatherSummary.Condition, .. WeatherSummary.Temperature], m => Assert.False(m.Graph));
    }

    [Fact]
    public void S15025_the_shares_tile_the_summarys_rect()
    {
        var shares = new[] { WeatherSummary.SkyShare, WeatherSummary.TemperatureShare, WeatherSummary.ConditionShare };

        Assert.Equal(1.0, shares.Sum(s => s.Width * s.Height), 9);
        Assert.Equal(1.0, WeatherSummary.SkyShare.Width + WeatherSummary.TemperatureShare.Width, 9);
        Assert.Equal(WeatherSummary.TemperatureShare.X, WeatherSummary.ConditionShare.X);
        Assert.Equal(WeatherSummary.TemperatureShare.Height, WeatherSummary.ConditionShare.Y);
    }

    [Fact]
    public void S15020_a_copy_keeps_its_kind_and_its_place()
    {
        var copy = new WeatherConditionConfig { Latitude = 1, Longitude = 2, LocationName = "Here" }.Clone();

        var condition = Assert.IsType<WeatherConditionConfig>(copy);
        Assert.Equal((1.0, 2.0, "Here"), (condition.Latitude!.Value, condition.Longitude!.Value, condition.LocationName));
        Assert.IsType<WeatherSkyConfig>(new WeatherSkyConfig().Clone());
    }
}
