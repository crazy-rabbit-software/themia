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
/// [S-6070]..[S-6074] A settings page offers only the rows that change what its widget shows in
/// its current style and state: one Core rule per config (<see cref="IShownSettings{TSetting}"/>),
/// pinned here per kind and style, and the page shape that rebuilds the page when it changes.
/// </summary>
public class SettingRulesTests
{
    [Theory]
    [InlineData(ClockStyle.Digital, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.TextSize, ClockSetting.Label, ClockSetting.Offset })]
    [InlineData(ClockStyle.Analog, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.TextSize, ClockSetting.Label, ClockSetting.Offset })]
    [InlineData(ClockStyle.Stacked, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.TextSize, ClockSetting.Label, ClockSetting.Offset })]
    [InlineData(ClockStyle.Flip, new[] { ClockSetting.Seconds, ClockSetting.Date, ClockSetting.TextSize, ClockSetting.Label, ClockSetting.Offset })]
    [InlineData(ClockStyle.Word, new[] { ClockSetting.Date, ClockSetting.TextSize, ClockSetting.Label, ClockSetting.Offset })]
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
    public void S6074_reshuffle_is_offered_with_the_shuffle_only()
    {
        Assert.Empty(new QuoteConfig { Shuffle = false }.ShownSettings);
        Assert.Equal([QuoteSetting.Reshuffle], new QuoteConfig { Shuffle = true }.ShownSettings);
    }

    public static TheoryData<GallerySource, GalleryStyle, bool, GallerySetting[]> GalleryRows => new()
    {
        { GallerySource.Folder, GalleryStyle.Slideshow, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.SlideSeconds, GallerySetting.Fit, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
        { GallerySource.Folder, GalleryStyle.Grid, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.Fit, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
        { GallerySource.Folder, GalleryStyle.Scatter, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
        { GallerySource.Folder, GalleryStyle.Mosaic, false, [GallerySetting.Folder, GallerySetting.Recursive, GallerySetting.Count, GallerySetting.Shuffle, GallerySetting.OpenFolder] },
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
        Assert.Equal([ShortcutsSetting.RowSize, ShortcutsSetting.Extensions], new ShortcutsConfig { Style = ShortcutsStyle.List, GridIconOnly = true }.ShownSettings);
    }

    [Theory]
    [InlineData("cpu")]
    [InlineData("memory")]
    [InlineData("gpu")]
    [InlineData("disk")]
    [InlineData("network")]
    [InlineData("battery")]
    [InlineData("ping")]
    public void S6074_a_meter_offers_its_chart_span_only_in_the_styles_that_draw_a_history(string kindId)
    {
        var kind = new WidgetId(kindId);
        var gauge = Assert.IsAssignableFrom<GaugeConfig>(WidgetConfig.CreateDefault(kind));

        foreach (var style in WidgetStyles.For(kind))
        {
            WidgetStyles.Set(gauge, style.Value);
            Assert.Equal(style.Value, WidgetStyles.Current(gauge));

            // The graph, and the CPU's core graphs ([S-6317]): a history per core.
            var history = style.Value == 0 || (kind == CpuConfig.Descriptor.Id && style.Value == (int)CpuStyle.CoreGraphs);
            Assert.Equal(history ? [MeterSetting.Span] : (MeterSetting[])[], gauge.ShownSettings);
        }
    }

    [Fact]
    public void S6074_the_first_style_of_every_meter_is_its_graph()
    {
        Assert.Equal(0, (int)CpuStyle.Graph);
        Assert.Equal(0, (int)MemoryStyle.Graph);
        Assert.Equal(0, (int)GpuStyle.Graph);
        Assert.Equal(0, (int)DiskStyle.Graph);
        Assert.Equal(0, (int)NetworkStyle.Graph);
        Assert.Equal(0, (int)BatteryStyle.Graph);
        Assert.Equal(0, (int)PingStyle.Graph);
    }

    [Theory]
    [InlineData(WeatherNowStyle.Standard, true)]
    [InlineData(WeatherNowStyle.LargeIcon, false)]
    [InlineData(WeatherNowStyle.Minimal, false)]
    public void S6074_the_readings_line_is_the_standard_weather_styles_own(WeatherNowStyle style, bool details)
        => Assert.Equal(details, new WeatherNowConfig { Style = style }.Offers(WeatherNowSetting.Details));

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
    [InlineData(SunStyle.Arc, true)]
    [InlineData(SunStyle.DayBar, true)]
    [InlineData(SunStyle.Times, false)]
    public void S6074_the_times_style_is_the_events_so_it_offers_no_switch_for_them(SunStyle style, bool events)
        => Assert.Equal(events, new SunConfig { Style = style }.Offers(SunSetting.Events));

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
    public void S6071_the_page_shape_follows_the_style_and_a_parent_switch_but_not_a_plain_value()
    {
        var state = AppState.CreateDefault();
        var widget = WidgetInstance.Create(QuoteConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.2, 0.2));
        state.Views[0].Widgets.Add(widget);
        var quote = (QuoteConfig)widget.Config;

        string Shape() => SettingsPageSync.Shape(state, SettingsScope.Widget, id: widget.Id);
        var before = Shape();

        quote.ShowAuthor = !quote.ShowAuthor;
        quote.Seed++;
        Assert.Equal(before, Shape());

        quote.Shuffle = !quote.Shuffle;
        var shuffled = Shape();
        Assert.NotEqual(before, shuffled);

        quote.Style = quote.Style == QuoteStyle.Plain ? QuoteStyle.Marks : QuoteStyle.Plain;
        Assert.NotEqual(shuffled, Shape());
    }

    [Fact]
    public void S6071_a_slider_never_reshapes_the_gallery_page_under_the_pointer()
    {
        var gallery = new GalleryConfig { Source = GallerySource.File };
        var shape = SettingRules.Shape(gallery);

        gallery.Rotation = 45;
        gallery.OutlineWidth = gallery.EffectiveOutlineWidth + 1;
        Assert.Equal(shape, SettingRules.Shape(gallery));
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
