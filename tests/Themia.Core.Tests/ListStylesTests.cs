using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6400]..[S-6410] The styles of the list and media widgets: Tasks, Shortcuts, Gallery, Mail,
/// Quote, Now playing and Habits. Each was a look option before ([S-6000]); these pin what each
/// style is, and the rules a new look brought (the large mark, the heatmap, the checklist).
/// </summary>
public class ListStylesTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Theory]
    [InlineData(WidgetKind.Tasks, "list,compact")]
    [InlineData(WidgetKind.Shortcuts, "grid,list,details")]
    [InlineData(WidgetKind.Gallery, "slideshow,grid,scatter,mosaic")]
    [InlineData(WidgetKind.Email, "list,compact,table")]
    [InlineData(WidgetKind.Quote, "plain,marks")]
    [InlineData(WidgetKind.Music, "artwork,cover,text")]
    [InlineData(WidgetKind.HabitTracker, "dots,heatmap,checklist")]
    public void S6400_each_kind_offers_its_styles_in_order_the_old_default_first(WidgetKind kind, string names)
    {
        Assert.True(WidgetStyles.Offers(kind));
        Assert.Equal(names, string.Join(",", WidgetStyles.For(kind).Select(s => s.Name)));
        Assert.Equal(WidgetStyles.For(kind)[0].Value, WidgetStyles.Current(WidgetConfig.CreateDefault(kind)));
    }

    [Fact]
    public void S6401_only_the_grid_draws_with_the_tile_settings()
    {
        Assert.True(new ShortcutsConfig { Style = ShortcutsStyle.Grid }.UsesTileSettings);
        Assert.False(new ShortcutsConfig { Style = ShortcutsStyle.List }.UsesTileSettings);
        Assert.False(new ShortcutsConfig { Style = ShortcutsStyle.Details }.UsesTileSettings);
    }

    [Fact]
    public void S6404_plain_quotes_in_the_language_marks_and_large_marks_leave_the_text_bare()
    {
        Assert.Equal("“Less is more.”", QuoteLayout.Set(Locale.En, " Less is more. ", QuoteStyle.Plain));
        Assert.Equal("„Weniger ist mehr.“", QuoteLayout.Set(Locale.De, "Weniger ist mehr.", QuoteStyle.Plain));
        Assert.Equal("Less is more.", QuoteLayout.Set(Locale.En, " Less is more. ", QuoteStyle.Marks));
        Assert.Equal(string.Empty, QuoteLayout.Set(Locale.En, "   ", QuoteStyle.Marks));
    }

    [Theory]
    [InlineData(120, 76, 28)]
    [InlineData(320, 196, 28)]
    [InlineData(560, 376, 40)]
    [InlineData(800, 516, 68)]
    public void S6405_the_large_mark_is_a_ramp_step_chosen_by_the_smaller_side(double width, double height, double size)
        => Assert.Equal(size, QuoteLayout.MarkSize(new WidgetArea(width, height)));

    [Fact]
    public void S6406_text_only_draws_and_fetches_no_art()
    {
        Assert.True(new MusicConfig { Style = MusicStyle.Artwork }.ShowsArt);
        Assert.True(new MusicConfig { Style = MusicStyle.Cover }.ShowsArt);
        Assert.False(new MusicConfig { Style = MusicStyle.Text }.ShowsArt);
    }

    [Fact]
    public void S6408_only_the_dots_draw_the_run_and_the_history()
    {
        Assert.True(new HabitTrackerConfig { Style = HabitTrackerStyle.Dots }.UsesRowExtras);
        Assert.False(new HabitTrackerConfig { Style = HabitTrackerStyle.Heatmap }.UsesRowExtras);
        Assert.False(new HabitTrackerConfig { Style = HabitTrackerStyle.Checklist }.UsesRowExtras);
    }

    // ---- [S-6409] heatmap ---------------------------------------------------------------------

    [Fact]
    public void S6409_a_daily_grid_is_calendar_weeks_from_the_first_day_with_the_rest_of_this_week_blank()
    {
        var cadence = new HabitCadence(HabitPeriod.Daily);
        var wednesday = HabitPeriods.At(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero), cadence, Utc);

        var cells = HabitHeatmap.Cells(wednesday, cadence, Utc, 3, DayOfWeek.Monday);

        Assert.Equal(21, cells.Count);
        Assert.Equal("2026-10-12", cells[14]!.Value.Key);          // Monday, top of the last column
        Assert.Equal("2026-10-14", cells[16]!.Value.Key);          // today
        Assert.All(cells.Skip(17), c => Assert.Null(c));           // Thursday .. Sunday to come
        Assert.Equal("2026-09-28", cells[0]!.Value.Key);           // two Mondays back
        Assert.Equal("2026-10-11", cells[13]!.Value.Key);          // the Sunday before

        var sunday = HabitHeatmap.Cells(wednesday, cadence, Utc, 1, DayOfWeek.Sunday);
        Assert.Equal("2026-10-11", sunday[0]!.Value.Key);
        Assert.Equal("2026-10-14", sunday[3]!.Value.Key);
    }

    [Fact]
    public void S6409_any_other_cadence_fills_to_the_last_cell_one_period_a_step()
    {
        var cadence = new HabitCadence(HabitPeriod.Weekly);
        var now = HabitPeriods.At(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero), cadence, Utc);

        var cells = HabitHeatmap.Cells(now, cadence, Utc, 2, DayOfWeek.Monday);

        Assert.Equal(now.Key, cells[^1]!.Value.Key);
        Assert.All(cells, c => Assert.NotNull(c));
        Assert.Equal(HabitPeriods.Previous(now, cadence, Utc).Key, cells[^2]!.Value.Key);
        Assert.Equal(14, cells.Select(c => c!.Value.Key).Distinct().Count());
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(2, 5, 2)]
    [InlineData(3, 5, 3)]
    [InlineData(4, 5, 4)]
    [InlineData(5, 5, 4)]
    [InlineData(1, 1, 4)]
    [InlineData(3, 0, 0)]
    public void S6409_a_shared_cell_is_shaded_by_the_share_done_rounded_up(int done, int count, int level)
        => Assert.Equal(level, HabitHeatmap.Level(done, count));

    [Fact]
    public void S6409_a_few_habits_on_a_large_tile_get_a_grid_each()
    {
        var plan = HabitHeatmap.Plan(new WidgetArea(528, 360), habits: 3, nameLine: 20);

        Assert.True(plan.PerHabit);
        Assert.InRange(plan.Cell, HabitHeatmap.MinCell, HabitHeatmap.MaxCell);
        Assert.True((3 * (20 + HabitHeatmap.NameGap + plan.GridHeight)) + (2 * HabitHeatmap.BlockGap) <= 360);
        Assert.True((plan.Weeks * plan.Pitch) - HabitHeatmap.Gap <= 528);
    }

    [Fact]
    public void S6409_a_long_list_shares_one_grid_shaded_by_how_many_were_done()
    {
        var plan = HabitHeatmap.Plan(new WidgetArea(528, 360), habits: 10, nameLine: 20);

        Assert.False(plan.PerHabit);
        Assert.Equal(HabitHeatmap.MaxCell, plan.Cell);
        Assert.Equal(24, plan.Weeks);
    }

    [Theory]
    [InlineData(104, 60)]
    [InlineData(168, 90)]
    [InlineData(288, 180)]
    [InlineData(448, 100)]
    [InlineData(168, 340)]
    [InlineData(528, 360)]
    [InlineData(768, 500)]
    public void S6409_every_tier_draws_at_least_a_month_inside_the_tile(double width, double height)
    {
        foreach (var habits in new[] { 1, 3, 10 })
        {
            var plan = HabitHeatmap.Plan(new WidgetArea(width, height), habits, 20);

            Assert.InRange(plan.Weeks, HabitHeatmap.MinWeeks, HabitHeatmap.MaxWeeks);
            Assert.InRange(plan.Cell, HabitHeatmap.MinCell, HabitHeatmap.MaxCell);
            Assert.True((plan.Weeks * plan.Pitch) - HabitHeatmap.Gap <= width + 0.01, $"{width}x{height}, {habits}: too wide");
        }
    }

    [Fact]
    public void S6409_a_huge_tile_stops_at_a_year()
        => Assert.Equal(HabitHeatmap.MaxWeeks, HabitHeatmap.Plan(new WidgetArea(4000, 100), 1, 20).Weeks);

    [Fact]
    public void S6409_nothing_to_draw_is_an_empty_plan()
    {
        Assert.Equal(0, HabitHeatmap.Plan(new WidgetArea(320, 200), 0, 20).Weeks);
        Assert.Empty(HabitHeatmap.Cells(default, new HabitCadence(HabitPeriod.Daily), Utc, 0, DayOfWeek.Monday));
    }

    // ---- [S-6410] checklist -------------------------------------------------------------------

    [Theory]
    [InlineData(120, 76)]
    [InlineData(200, 106)]
    [InlineData(320, 196)]
    [InlineData(480, 116)]
    [InlineData(200, 356)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    public void S6410_a_checklist_is_the_box_and_the_name_at_every_size(double width, double height)
    {
        var dots = HabitLayout.Plan(new WidgetArea(width, height), new HabitLayoutInput(5, true, true, 7), 40, 32);
        var plan = HabitLayout.Plan(new WidgetArea(width, height), new HabitLayoutInput(5, true, true, 7, Checklist: true), 40, 32);

        Assert.False(plan.Streak);
        Assert.Equal(0, plan.Dots);
        Assert.False(plan.Rate);
        Assert.False(plan.Best);
        Assert.Equal(dots.Rows, plan.Rows);
        Assert.Equal(dots.AddRow, plan.AddRow);
    }
}
