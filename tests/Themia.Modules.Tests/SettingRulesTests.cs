using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6070] [S-6074] A widget's properties menu offers only the options that change what it shows
/// in its current style and state: one Core rule per config (<see cref="IShownSettings{TSetting}"/>),
/// pinned here per kind and style.
/// </summary>
public class SettingRulesTests
{
    [Theory]
    [InlineData(ClockStyle.Digital, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.Offset })]
    [InlineData(ClockStyle.Analog, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.Offset })]
    [InlineData(ClockStyle.Stacked, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.Offset })]
    [InlineData(ClockStyle.Flip, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.Offset })]
    [InlineData(ClockStyle.Word, new[] { ClockSetting.Date, ClockSetting.Offset })]
    [InlineData(ClockStyle.Minimal, new ClockSetting[0])]
    [InlineData(ClockStyle.Date, new ClockSetting[0])]
    public void S6074_the_clock_offers_the_lines_its_style_draws(ClockStyle style, ClockSetting[] rows)
        => Assert.Equal(rows, new ClockConfig { Style = style }.ShownSettings);

    [Fact]
    public void S6074_a_clock_offers_seconds_exactly_where_it_can_draw_them()
    {
        foreach (var style in Enum.GetValues<ClockStyle>())
        {
            var clock = new ClockConfig { Style = style, ShowSeconds = true };
            Assert.Equal(clock.DrawsSeconds, clock.Offers(ClockSetting.Seconds));
        }
    }

    [Fact]
    public void S6074_reshuffle_is_offered_with_the_shuffle_only_and_the_rotation_with_two_quotes()
    {
        Assert.Equal([QuoteSetting.Shuffle, QuoteSetting.Cadence], new QuoteConfig { Shuffle = false }.ShownSettings);
        Assert.Equal([QuoteSetting.Shuffle, QuoteSetting.Reshuffle, QuoteSetting.Cadence], new QuoteConfig { Shuffle = true }.ShownSettings);
        Assert.Empty(new QuoteConfig { Shuffle = true, Quotes = [new() { Text = "Only" }] }.ShownSettings);
    }

    public static TheoryData<GallerySource, GalleryStyle, bool, GallerySetting[]> GalleryRows => new()
    {
        { GallerySource.Folder, GalleryStyle.Slideshow, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.SlideSeconds, GallerySetting.Fit, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
        { GallerySource.Folder, GalleryStyle.Grid, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.Fit, GallerySetting.Cells, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
        { GallerySource.Folder, GalleryStyle.Scatter, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.Shown, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
        { GallerySource.Folder, GalleryStyle.Mosaic, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.Shown, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
        { GallerySource.File, GalleryStyle.Mosaic, false, [GallerySetting.File, GallerySetting.Fit, GallerySetting.Rotation, GallerySetting.Outline] },
        { GallerySource.File, GalleryStyle.Slideshow, true, [GallerySetting.File, GallerySetting.Rotation, GallerySetting.Outline, GallerySetting.OutlineColor, GallerySetting.OutlineWidth] },
        { GallerySource.Bing, GalleryStyle.Grid, true, [GallerySetting.Fit] },
    };

    [Theory]
    [MemberData(nameof(GalleryRows))]
    public void S6074_the_gallery_offers_what_its_source_layout_and_outline_use(GallerySource source, GalleryStyle style, bool outline, GallerySetting[] rows)
        => Assert.Equal(rows, new GalleryConfig { Source = source, Style = style, Outline = outline }.ShownSettings);

    [Fact]
    public void S6074_a_grid_of_icons_alone_draws_no_names_so_no_extensions()
    {
        Assert.Contains(ShortcutsSetting.Extensions, new ShortcutsConfig { Style = ShortcutsStyle.Grid }.ShownSettings);
        Assert.DoesNotContain(ShortcutsSetting.Extensions, new ShortcutsConfig { Style = ShortcutsStyle.Grid, GridIconOnly = true }.ShownSettings);
        Assert.Equal([ShortcutsSetting.Lines, ShortcutsSetting.Extensions], new ShortcutsConfig { Style = ShortcutsStyle.List, GridIconOnly = true }.ShownSettings);
    }

    [Theory]
    [InlineData(QuoteMarket.Listed, QuoteRange.Day, StocksStyle.Chart, new[] { StocksSetting.MarketState, StocksSetting.ExtendedHours, StocksSetting.MinMax })]
    [InlineData(QuoteMarket.Listed, QuoteRange.FiveDays, StocksStyle.Number, new[] { StocksSetting.MarketState, StocksSetting.ExtendedHours })]
    [InlineData(QuoteMarket.Listed, QuoteRange.Month, StocksStyle.Chart, new[] { StocksSetting.MarketState, StocksSetting.MinMax })]
    [InlineData(QuoteMarket.Crypto, QuoteRange.Day, StocksStyle.Chart, new[] { StocksSetting.MinMax })]
    [InlineData(QuoteMarket.Crypto, QuoteRange.Year, StocksStyle.Number, new StocksSetting[0])]
    public void S6074_the_stock_offers_what_its_market_period_and_style_use(QuoteMarket market, QuoteRange range, StocksStyle style, StocksSetting[] rows)
        => Assert.Equal(rows, new StocksConfig { Market = market, Range = range, Style = style }.ShownSettings);

    [Fact]
    public void S6074_extended_hours_are_offered_exactly_where_they_are_asked_for()
    {
        foreach (var market in Enum.GetValues<QuoteMarket>())
        {
            foreach (var range in Enum.GetValues<QuoteRange>())
            {
                var stocks = new StocksConfig { Market = market, Range = range, ShowExtendedHours = true };
                Assert.Equal(stocks.WantsPrePost, stocks.Offers(StocksSetting.ExtendedHours));
            }
        }
    }

    [Theory]
    [InlineData(MoonStyle.Disc, true)]
    [InlineData(MoonStyle.PhaseMonth, false)]
    public void S6074_the_month_of_phases_reads_no_longitude(MoonStyle style, bool longitude)
        => Assert.Equal(longitude, new MoonConfig { Style = style }.Offers(MoonSetting.Longitude));

    [Fact]
    public void S6074_the_planner_offers_the_task_account_while_its_tasks_show()
    {
        Assert.Equal([WeekPlannerSetting.TaskAccount], new WeekPlannerConfig { ShowTasks = true }.ShownSettings);
        Assert.Empty(new WeekPlannerConfig { ShowTasks = false }.ShownSettings);
    }

    [Fact]
    public void S6074_due_dates_and_importance_are_an_accounts()
    {
        var account = new TasksConfig { Source = TaskSource.Account, ShowDueDate = true };
        Assert.Equal([TasksSetting.DueDate, TasksSetting.DueFilter, TasksSetting.ImportantOnly], account.ShownSettings);

        var local = new TasksConfig { Source = TaskSource.Local, ShowDueDate = true };
        Assert.Empty(local.ShownSettings);
    }

    [Fact]
    public void S6074_habits_offer_the_minutes_under_the_custom_period_and_the_run_on_the_dots()
    {
        Assert.Equal([HabitsSetting.Interval, HabitsSetting.Streak, HabitsSetting.History],
            new HabitTrackerConfig { Period = HabitPeriod.Custom, Style = HabitTrackerStyle.Dots }.ShownSettings);
        Assert.Equal([HabitsSetting.Interval], new HabitTrackerConfig { Period = HabitPeriod.Custom, Style = HabitTrackerStyle.Checklist }.ShownSettings);
        Assert.Empty(new HabitTrackerConfig { Period = HabitPeriod.Daily, Style = HabitTrackerStyle.Heatmap }.ShownSettings);
    }

    [Fact]
    public void S6074_a_volume_is_offered_while_a_sound_is_chosen()
    {
        Assert.Empty(new PomodoroConfig { Sound = AlarmSound.Off }.ShownSettings);
        Assert.Equal([SoundSetting.Volume], new PomodoroConfig { Sound = AlarmSound.Chime }.ShownSettings);
        Assert.Empty(new AlarmConfig { Sound = AlarmSound.Off }.ShownSettings);
        Assert.Equal([SoundSetting.Volume], new AlarmConfig { Sound = AlarmSound.Bell }.ShownSettings);
    }

    [Theory]
    [InlineData(ContactsLayout.List, false)]
    [InlineData(ContactsLayout.Compact, false)]
    [InlineData(ContactsLayout.Details, true)]
    [InlineData(ContactsLayout.Grid, false)]
    public void S6074_the_columns_are_the_contacts_tables(ContactsLayout layout, bool columns)
        => Assert.Equal(columns, new ContactsConfig { Layout = layout }.Offers(ContactsSetting.Columns));

    [Fact]
    public void S6074_each_screen_time_widget_offers_the_limit_it_draws_and_the_list_its_average_over_a_window()
    {
        Assert.Equal([ScreenTimeSetting.DailyLimit], new ScreenTimeChartConfig().ShownSettings);
        Assert.Equal([ScreenTimeSetting.DailyLimit], new ScreenTimeDonutConfig().ShownSettings);
        Assert.Equal([ScreenTimeSetting.AppLimits], new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Day }.ShownSettings);
        Assert.Equal([ScreenTimeSetting.AppLimits, ScreenTimeSetting.Average], new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Week }.ShownSettings);
    }

    [Fact]
    public void S6070_the_rule_is_never_written_to_the_file()
    {
        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
        {
            var json = JsonSerializer.Serialize(WidgetConfig.CreateDefault(kind), StateJson.WidgetConfig);
            Assert.DoesNotContain("shownSettings", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
