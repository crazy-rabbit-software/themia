using Themia.App.Shell.Config;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeatherFacts.Tests;

/// <summary>[S-13910]..[S-13921] The weather forecast and the day parts it is put together of.</summary>
public sealed class WeatherDaysTests
{
    private static readonly ScreenMetrics Screen = new(1920, 1080);

    private static WidgetConfig New(string kind) => WidgetConfig.CreateDefault(new WidgetId(kind));

    /// <summary>A new forecast at (0.1, 0.1) with the parts it starts with.</summary>
    private static (ViewState View, WidgetInstance Forecast, WeatherDaysConfig Config, List<WidgetInstance> Parts) Placed()
    {
        var view = ViewState.Create("v");
        var forecast = WidgetInstance.Create(new WidgetId("weatherDays"), new FracRect(0.1, 0.1, 0.2, 0.2));
        view.Widgets.Add(forecast);
        var config = (WeatherDaysConfig)forecast.Config;
        var parts = Repeaters.Seed(view, new RepeaterOn(forecast, config), Screen);
        return (view, forecast, config, parts);
    }

    [Fact]
    public void S13915_a_new_forecast_starts_with_the_day_its_sky_its_low_its_range_and_its_high_side_by_side()
    {
        var (view, forecast, config, parts) = Placed();

        Assert.Equal(["weatherDay", "weatherSky", "weatherLow", "weatherRange", "weatherHigh"], parts.Select(p => p.Kind.Value));
        Assert.Equal(parts.Select(p => p.Id), config.Members);
        Assert.Equal(forecast.Rect.X, parts[0].Rect.X, 9);
        Assert.All(parts, p => Assert.Equal(forecast.Rect.Y, p.Rect.Y, 9));
        for (var i = 0; i + 1 < parts.Count; i++)
        {
            Assert.Equal(parts[i].Rect.Right, parts[i + 1].Rect.X, 9);
        }

        // Each part stands at the size it was given: none is under its own least on the desktop's grid.
        Assert.All(parts, p => Assert.Equal(p.Rect, SizeFit.Shown(p.Rect, SizeFit.Limits(p, Screen, 1), Screen)));

        // The day's name starts its row and the low ends at the bar it leads.
        Assert.Equal((ContentAlign.Left, ContentAlign.Right), (parts[0].Align, parts[2].Align));
        Assert.Empty(Repeaters.Repair(new AppState { Views = [view] }));
    }

    [Fact]
    public void S13916_a_new_forecasts_width_is_its_days_and_its_height_a_week_of_them()
    {
        var (_, forecast, config, parts) = Placed();
        var (cols, rows) = GridGeometry.GridDims(Screen);

        Assert.Equal((37, 4), (config.DayWidth, config.DayHeight));
        Assert.Equal(37.0 / cols, forecast.Rect.W, 9);
        Assert.Equal(parts[^1].Rect.Right, forecast.Rect.Right, 9);
        Assert.Equal(7 * 4.0 / rows, forecast.Rect.H, 9);
    }

    [Fact]
    public void S13916_resizing_the_forecast_resizes_its_day_and_its_parts_still_touch()
    {
        var (view, forecast, config, parts) = Placed();
        var (cols, _) = GridGeometry.GridDims(Screen);
        forecast.Rect = forecast.Rect with { W = 60.0 / cols };

        Assert.True(Repeaters.Lay(view, Screen));

        Assert.True(config.DayWidth > 37);
        Assert.Equal(config.DayWidth / (double)cols, forecast.Rect.W, 9);
        Assert.Equal(forecast.Rect.Right, parts[^1].Rect.Right, 9);
        for (var i = 0; i + 1 < parts.Count; i++)
        {
            Assert.Equal(parts[i].Rect.Right, parts[i + 1].Rect.X, 9);
        }

        // Narrower than its parts can go, it stops at their least and stays as wide as its day.
        forecast.Rect = forecast.Rect with { W = 3.0 / cols };
        Repeaters.Lay(view, Screen);
        Assert.Equal(config.DayWidth / (double)cols, forecast.Rect.W, 9);
        Assert.All(parts, p => Assert.Equal(p.Rect, SizeFit.Shown(p.Rect, SizeFit.Limits(p, Screen, 1), Screen)));
    }

    [Fact]
    public void S13910_it_has_no_background_of_its_own()
    {
        var (view, forecast, _, _) = Placed();
        forecast.Background = new FillChoice();

        Repeaters.Lay(view, Screen);

        Assert.Equal(FillKind.Transparent, forecast.Background.Kind);
    }

    [Theory]
    [InlineData("weatherDay", true)]
    [InlineData("weatherSky", true)]
    [InlineData("weatherHigh", true)]
    [InlineData("weatherLow", true)]
    [InlineData("weatherRange", true)]
    [InlineData("weatherHighLow", true)]
    [InlineData("weatherWind", true)]
    [InlineData("weatherSunrise", true)]
    [InlineData("text", true)]
    [InlineData("icon", true)]
    [InlineData("divider", true)]
    [InlineData("weatherHumidity", false)] // a reading of the present
    [InlineData("weatherCondition", false)] // the sky now in words
    [InlineData("clock", false)]
    [InlineData("header", false)]
    [InlineData("weatherDays", false)] // never another forecast
    public void S13910_its_day_takes_what_a_day_can_show_and_the_design_elements(string kind, bool takes)
        => Assert.Equal(takes, new WeatherDaysConfig().Accepts(New(kind)));

    [Fact]
    public void S13911_its_page_is_its_place_then_its_layout_spacing_first_day_and_days()
    {
        var menu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new WeatherDaysModule()).MenuLines(menu, new WeatherDaysConfig());

        var place = WeatherPlaceRows.Lines(new WidgetMenuLines(() => { }), new WeatherDaysConfig()).Select(l => l.Text).ToList();
        Assert.Equal([.. place, "Layout", "Spacing", "Starts", "Days"], menu.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));

        // A grid sets its rows' and its columns' spacing apart.
        var grid = new WidgetMenuLines(() => { });
        ((IWidgetModule)new WeatherDaysModule()).MenuLines(grid, new WeatherDaysConfig { Direction = RepeatDirection.Wrap });
        Assert.Equal([.. place, "Layout", "Row spacing", "Column spacing", "Starts", "Days"], grid.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S13918_it_shows_a_week_by_default_and_a_count_it_cannot_show_is_a_week_after_repair()
    {
        Assert.Equal(7, new WeatherDaysConfig().Count);
        Assert.Equal(7, Themia.Core.Series.SeriesSources.ValuesOf(new WeatherDaysConfig()).Count);
        Assert.Equal(["2", "3", "4"], Themia.Core.Series.SeriesSources.ValuesOf(new WeatherDaysConfig { Start = 2, Days = 3 }));

        foreach (var days in new[] { 0, -2, 15 })
        {
            var config = new WeatherDaysConfig { Days = days };
            Assert.NotEmpty(config.Repair());
            Assert.Equal(WeatherDaysConfig.DefaultDays, config.Days);
        }

        var copy = (WeatherDaysConfig)new WeatherDaysConfig { Days = 10, Start = 1, DayWidth = 30, DayHeight = 4, Direction = RepeatDirection.Across, Latitude = 1, Longitude = 2 }.Clone();
        Assert.Equal((10, 1, 30, 4, RepeatDirection.Across), (copy.Days, copy.Start, copy.DayWidth, copy.DayHeight, copy.Direction));
        Assert.Equal(1, copy.Latitude);
    }

    private static CurrentWeather Reading()
    {
        var today = new DateOnly(2026, 11, 21);
        return new CurrentWeather(9, 8, 84, 14, 3)
        {
            Days =
            [
                new ForecastDay(today, 61, 6, 13, 2.6),
                new ForecastDay(today.AddDays(1), 0, 2, 16, 0),
                new ForecastDay(today.AddDays(2), 3, 4, null, 0),
            ],
        };
    }

    [Fact]
    public void S13919_a_day_part_reads_its_day_and_a_day_past_the_forecast_reads_nothing()
    {
        var reading = Reading();

        Assert.Equal(["Today", "Sun"], new[] { 0, 1 }.Select(d => DayParts.Text(Locale.En, DayPart.Day, reading, d)));
        Assert.Equal(["13°", "16°"], new[] { 0, 1 }.Select(d => DayParts.Text(Locale.En, DayPart.High, reading, d)));
        Assert.Equal(["6°", "2°"], new[] { 0, 1 }.Select(d => DayParts.Text(Locale.En, DayPart.Low, reading, d)));
        Assert.Equal(WeatherFactRules.Missing, DayParts.Text(Locale.En, DayPart.High, reading, 2)); // the service sent no high
        Assert.Equal(WeatherFactRules.Missing, DayParts.Text(Locale.En, DayPart.Day, reading, 9));

        Assert.NotNull(DayParts.SkyGlyph(reading, 0));
        Assert.NotEqual(DayParts.SkyGlyph(reading, 0), DayParts.SkyGlyph(reading, 1));
        Assert.Null(DayParts.SkyGlyph(reading, 9));
    }

    [Fact]
    public void S13920_a_days_range_stands_on_the_scale_of_every_day_the_forecast_holds()
    {
        var reading = Reading(); // lows 6, 2, 4 and highs 13, 16: the scale is 2..16

        var today = DayParts.Range(reading, 0)!.Value;
        Assert.Equal((4 / 14.0, 11 / 14.0), (today.From, today.To));
        Assert.Equal((0d, 1d), DayParts.Range(reading, 1)!.Value);
        Assert.Null(DayParts.Range(reading, 2)); // no high
        Assert.Null(DayParts.Range(reading, 9));

        // One day, or every day alike: the whole track.
        var flat = new CurrentWeather(9, 8, 84, 14, 3) { Days = [new ForecastDay(new DateOnly(2026, 11, 21), 0, 5, 5, 0)] };
        Assert.Equal((0d, 1d), DayParts.Range(flat, 0)!.Value);
    }

    [Fact]
    public void S13921_a_day_part_is_a_small_card_new_and_a_part_keeps_its_day_through_a_copy_and_repair()
    {
        foreach (var kind in new[] { "weatherDay", "weatherHigh", "weatherLow" })
        {
            var sizing = SizeFit.Of(New(kind));
            Assert.Equal(TextRules.ShortWidth, sizing.Natural.Width);
            Assert.True(sizing.Min.Width <= 60, $"{kind}: {sizing.Min.Width}");
            Assert.True(sizing.Min.Height <= 40, $"{kind}: {sizing.Min.Height}");
        }

        var part = new WeatherLowConfig { Day = 3, Latitude = 1, Longitude = 2 };
        var copy = (WeatherLowConfig)part.Clone();
        Assert.Equal((3, 1d), (copy.Day, copy.Latitude!.Value));
        part.Day = 99;
        Assert.NotEmpty(part.Repair());
        Assert.Equal(0, part.Day);
    }
}
