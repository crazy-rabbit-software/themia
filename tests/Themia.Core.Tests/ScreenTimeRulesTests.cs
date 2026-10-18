using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

public sealed class ScreenTimeRulesTests
{
    private static ScreenTimeApp App(string key, long seconds, string? path = null) => new(key, key.ToUpperInvariant(), seconds, path);

    private static ScreenTimeDay Day(string date, params ScreenTimeApp[] apps)
        => new(date, apps.Sum(a => a.Seconds), apps, 0);

    private static readonly HashSet<string> None = [];

    // ---- [S-452] durations, top apps, segments --------------------------------------------------

    [Theory]
    [InlineData((3 * 3600) + (24 * 60), "3h 24m")]
    [InlineData(3600, "1h 0m")]
    [InlineData((10 * 3600) + (59 * 60) + 59, "10h 59m")]
    [InlineData(60, "1m")]
    [InlineData((59 * 60) + 59, "59m")]
    [InlineData(59, "<1m")]
    [InlineData(0, "<1m")]
    [InlineData(-5, "<1m")]
    public void S452_durations(double seconds, string expected) => Assert.Equal(expected, ScreenTimeRules.FormatDuration(seconds));

    [Fact]
    public void S452_top_sorts_busiest_first_and_caps()
    {
        var apps = new[] { App("chrome", 5000), App("code", 9000), App("discord", 1200), App("terminal", 300) };

        Assert.Equal(["code", "chrome"], ScreenTimeRules.Top(apps, 2).Select(a => a.App));
        Assert.Empty(ScreenTimeRules.Top(apps, 0));
        Assert.Empty(ScreenTimeRules.Top(apps, -3));
        Assert.Equal(4, ScreenTimeRules.Top(apps, 99).Count);
    }

    [Theory]
    [InlineData(0, 12, 12)]   // fit: every app
    [InlineData(5, 12, 6)]    // ceiling 5 + an "Other" row
    [InlineData(5, 5, 5)]     // exactly the ceiling: no "Other"
    [InlineData(0, 0, 0)]
    public void S1211_the_list_wants_every_app_up_to_the_ceiling_plus_other(int maxApps, int apps, int wanted)
    {
        Assert.Equal(wanted, ScreenTimeRules.ListWanted(maxApps, apps));
    }

    [Theory]
    [InlineData(12, 12, 12)]  // room for all: all named, no "Other"
    [InlineData(10, 12, 9)]   // ten rows for twelve apps: nine named, the tenth is "Other"
    [InlineData(1, 12, 0)]    // one row: "Other" alone
    [InlineData(0, 12, 0)]
    [InlineData(4, 2, 2)]
    public void S3351_a_short_list_leaves_its_last_row_to_other(int rows, int apps, int named)
    {
        Assert.Equal(named, ScreenTimeRules.ListNamed(rows, apps));
    }

    [Theory]
    [InlineData(30, "<1m")]
    [InlineData(37 * 60, "37m")]
    [InlineData((4 * 3600) + (37 * 60), "4.6h")]
    [InlineData((9 * 3600) + (58 * 60), "10h")]
    [InlineData((12 * 3600) + (40 * 60), "13h")]
    [InlineData(double.NaN, "<1m")]
    public void S1210_compact_durations_drop_the_minutes(double seconds, string text)
    {
        Assert.Equal(text, ScreenTimeRules.CompactDuration(seconds));
    }

    [Fact]
    public void S1210_the_ring_text_is_never_wider_than_the_hole()
    {
        var seconds = (4 * 3600) + (37 * 60);

        // A large hole keeps the full duration at a large step.
        var (large, largeStep) = ScreenTimeRules.RingText(seconds, 200, 60);
        Assert.Equal("4h 37m", large);
        Assert.True(largeStep.FontSize > FluentType.Caption.FontSize);

        // The XS ring's hole (about 40 DIPs) cannot hold "4h 37m" even at Caption: it drops the minutes.
        var (small, smallStep) = ScreenTimeRules.RingText(seconds, 40, 20);
        Assert.Equal("4.6h", small);
        Assert.True(FluentType.EstimateWidth(smallStep, small.Length) <= 40);
    }

    [Fact]
    public void S452_segments_are_shares_with_cumulative_offsets()
    {
        var segments = ScreenTimeRules.Segments([App("a", 60), App("b", 30), App("c", 10)], 100, 5, "Other");

        Assert.Equal(["a", "b", "c"], segments.Select(s => s.Key));
        Assert.Equal([0.6, 0.3, 0.1], segments.Select(s => s.Fraction));
        Assert.Equal([0, 0.6, 0.9], segments.Select(s => s.Start));
        Assert.DoesNotContain(segments, s => s.IsOther);
    }

    [Fact]
    public void S452_apps_beyond_the_cap_fold_into_one_other_wedge()
    {
        var segments = ScreenTimeRules.Segments([App("a", 60), App("b", 30), App("c", 8), App("d", 2)], 100, 2, "Other");

        Assert.Equal(["a", "b", ScreenTimeRules.OtherKey], segments.Select(s => s.Key));
        var other = segments[2];
        Assert.True(other.IsOther);
        Assert.Equal("Other", other.Name);
        Assert.Equal(10, other.Seconds);
        Assert.Equal(0.1, other.Fraction, 6);
        Assert.Equal(0.9, other.Start, 6);
    }

    [Fact]
    public void S452_no_ring_without_a_total_and_no_negative_remainder()
    {
        Assert.Empty(ScreenTimeRules.Segments([App("a", 0)], 0, 5, "Other"));

        var over = ScreenTimeRules.Segments([App("a", 120)], 100, 5, "Other");
        Assert.Single(over);
        Assert.Equal(1, over[0].Fraction);
    }

    [Fact]
    public void S452_app_colours_are_stable_and_distinct()
    {
        Assert.Equal(ScreenTimeRules.Hue("chrome"), ScreenTimeRules.Hue("chrome"));
        Assert.NotEqual(ScreenTimeRules.Hue("chrome"), ScreenTimeRules.Hue("code"));
        Assert.NotEqual(ScreenTimeRules.Hue("discord"), ScreenTimeRules.Hue("spotify"));
        Assert.InRange(ScreenTimeRules.Hue(string.Empty), 0, 359);
        Assert.InRange(ScreenTimeRules.Hue("日本語"), 0, 359);
    }

    [Fact]
    public void S452_hsl_converts_the_primary_hues()
    {
        Assert.Equal((255, 0, 0), ScreenTimeRules.HslToRgb(0, 1, 0.5));
        Assert.Equal((0, 255, 0), ScreenTimeRules.HslToRgb(120, 1, 0.5));
        Assert.Equal((0, 0, 255), ScreenTimeRules.HslToRgb(240, 1, 0.5));
        Assert.Equal((128, 128, 128), ScreenTimeRules.HslToRgb(42, 0, 0.5));
    }

    [Fact]
    public void S452_initials_fall_back_to_a_question_mark()
    {
        Assert.Equal("G", ScreenTimeRules.Initial("Google Chrome"));
        Assert.Equal("C", ScreenTimeRules.Initial("code"));
        Assert.Equal("?", ScreenTimeRules.Initial("   "));
    }

    // ---- [S-455] hidden apps ----------------------------------------------------------------------

    [Fact]
    public void S455_hiding_an_app_rebases_the_total_and_the_ring()
    {
        var apps = new[] { App("code", 100), App("chrome", 60), App("explorer", 20) };
        var shown = ScreenTimeRules.Visible(apps, new HashSet<string> { "explorer" });

        Assert.Equal(180, ScreenTimeRules.Sum(apps));
        Assert.Equal(160, ScreenTimeRules.Sum(shown));
        Assert.Equal(0, ScreenTimeRules.Sum([App("x", -5)]));

        var segments = ScreenTimeRules.Segments(shown, ScreenTimeRules.Sum(shown), 5, "Other");
        Assert.Equal(["code", "chrome"], segments.Select(s => s.Key));
        Assert.Equal(100.0 / 160, segments[0].Fraction, 6);
    }

    // ---- [S-3321] [S-3330] period and style -----------------------------------------------------------

    [Fact]
    public void S3330_the_chart_draws_a_week_or_a_month()
    {
        Assert.Equal(ScreenTimePeriod.Week, ScreenTimeRules.ChartPeriod(ScreenTimePeriod.Day));
        Assert.Equal(ScreenTimePeriod.Week, ScreenTimeRules.ChartPeriod(ScreenTimePeriod.Week));
        Assert.Equal(ScreenTimePeriod.Month, ScreenTimeRules.ChartPeriod(ScreenTimePeriod.Month));
        Assert.Equal(ScreenTimePeriod.Week, ScreenTimeRules.ChartPeriod((ScreenTimePeriod)42));
        Assert.Equal(ScreenTimeChartStyle.Bars, ScreenTimeRules.ClampChartStyle((ScreenTimeChartStyle)42));
        Assert.Equal(ScreenTimeChartStyle.Heatmap, ScreenTimeRules.ClampChartStyle(ScreenTimeChartStyle.Heatmap));
    }

    [Fact]
    public void S3321_an_unknown_period_reads_as_a_day()
        => Assert.Equal(ScreenTimePeriod.Day, ScreenTimeRules.ClampPeriod((ScreenTimePeriod)42));

    [Fact]
    public void S451_windows_are_seven_and_thirty_days()
    {
        Assert.Equal(1, ScreenTimeRules.RangeLength(ScreenTimePeriod.Day));
        Assert.Equal(7, ScreenTimeRules.RangeLength(ScreenTimePeriod.Week));
        Assert.Equal(30, ScreenTimeRules.RangeLength(ScreenTimePeriod.Month));
    }

    // ---- [S-456] window drawings ---------------------------------------------------------------------

    private static readonly ScreenTimeDay[] Window =
    [
        Day("2026-07-01", App("code", 3600), App("chrome", 1800)),
        Day("2026-07-02", App("chrome", 600)),
        Day("2026-07-03", App("code", 1200), App("spotify", 300)),
    ];

    [Fact]
    public void S456_day_totals_rebase_without_hidden_apps_and_keep_zero_days()
    {
        var all = ScreenTimeRules.DayTotals(Window, None);
        Assert.Equal([5400L, 600L, 1500L], all.Select(d => d.Seconds));
        Assert.Equal(5400, ScreenTimeRules.Busiest(all));

        var noCode = ScreenTimeRules.DayTotals(Window, new HashSet<string> { "code" });
        Assert.Equal([1800L, 600L, 300L], noCode.Select(d => d.Seconds));
        Assert.Equal(0, ScreenTimeRules.Busiest([]));
    }

    [Fact]
    public void S456_aggregate_sums_apps_across_the_window_busiest_first()
    {
        var apps = ScreenTimeRules.Aggregate(Window, None);
        Assert.Equal(["code", "chrome", "spotify"], apps.Select(a => a.App));
        Assert.Equal([4800L, 2400L, 300L], apps.Select(a => a.Seconds));
        Assert.Equal(["chrome", "spotify"], ScreenTimeRules.Aggregate(Window, new HashSet<string> { "code" }).Select(a => a.App));
    }

    [Fact]
    public void S456_aggregate_keeps_the_first_known_path()
    {
        var apps = ScreenTimeRules.Aggregate(
            [Day("d1", App("code", 10)), Day("d2", App("code", 20, "C:/code.exe"))],
            None);

        Assert.Equal("C:/code.exe", apps.Single().Path);
        Assert.Equal(30, apps.Single().Seconds);
    }

    [Fact]
    public void S456_stacks_split_a_day_in_legend_order()
    {
        var legend = ScreenTimeRules.Segments(ScreenTimeRules.Aggregate(Window, None), 7500, 5, "Other");
        var parts = ScreenTimeRules.Stack(Window[0].Apps, legend, 5400);

        Assert.Equal(["code", "chrome"], parts.Select(p => p.Key));
        Assert.Equal(1, parts.Sum(p => p.Fraction), 6);

        var quiet = ScreenTimeRules.Stack(Window[1].Apps, legend, 5400);
        Assert.Equal(600.0 / 5400, quiet.Single().Fraction, 6);
    }

    [Fact]
    public void S456_apps_below_the_cap_fold_into_the_other_part()
    {
        var legend = ScreenTimeRules.Segments(ScreenTimeRules.Aggregate(Window, None), 7500, 1, "Other");
        Assert.Equal(["code", ScreenTimeRules.OtherKey], legend.Select(s => s.Key));

        var parts = ScreenTimeRules.Stack(Window[0].Apps, legend, 5400);
        Assert.Equal(["code", ScreenTimeRules.OtherKey], parts.Select(p => p.Key));
        Assert.Equal(1800, parts[1].Seconds);
        Assert.Equal(1, parts.Sum(p => p.Fraction), 6);
    }

    [Fact]
    public void S456_no_stack_for_an_empty_day_or_scale()
    {
        var legend = ScreenTimeRules.Segments(ScreenTimeRules.Aggregate(Window, None), 7500, 5, "Other");
        Assert.Empty(ScreenTimeRules.Stack([], legend, 5400));
        Assert.Empty(ScreenTimeRules.Stack(Window[0].Apps, legend, 0));
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(-5, 100, 0)]
    [InlineData(100, 100, 4)]
    [InlineData(1, 100_000, 1)]
    [InlineData(25, 100, 1)]
    [InlineData(26, 100, 2)]
    [InlineData(50, 100, 2)]
    [InlineData(75, 100, 3)]
    [InlineData(76, 100, 4)]
    [InlineData(10, 0, 0)]
    [InlineData(200, 100, 4)]
    public void S456_heat_levels(long seconds, long busiest, int expected)
        => Assert.Equal(expected, ScreenTimeRules.HeatLevel(seconds, busiest));

    [Fact]
    public void S456_the_heatmap_is_padded_so_the_window_ends_on_the_last_cell()
    {
        var cells = ScreenTimeRules.HeatmapCells(30);
        Assert.Equal(35, cells.Count);
        Assert.All(cells.Take(5), c => Assert.Null(c));
        Assert.Equal(0, cells[5]);
        Assert.Equal(29, cells[^1]);
        Assert.Equal(14, ScreenTimeRules.HeatmapCells(14).Count);
        Assert.Empty(ScreenTimeRules.HeatmapCells(0));
    }

    // ---- [S-453] averages, shares, switches -------------------------------------------------------

    [Fact]
    public void S453_the_average_divides_by_the_window_length()
    {
        Assert.Equal(3600, ScreenTimeRules.DailyAverage(7 * 3600, 7));
        Assert.Equal(3600.0 / 7, ScreenTimeRules.DailyAverage(3600, 7), 6);
        Assert.Equal(0, ScreenTimeRules.DailyAverage(3600, 0));
        Assert.Equal(0, ScreenTimeRules.DailyAverage(-60, 7));

        var perApp = new[] { 4800.0, 2400, 300 }.Sum(s => ScreenTimeRules.DailyAverage(s, 3));
        Assert.Equal(ScreenTimeRules.DailyAverage(7500, 3), perApp, 6);
    }

    [Fact]
    public void S453_share_is_clamped_and_zero_without_a_total()
    {
        Assert.Equal(0.5, ScreenTimeRules.Share(3600, 7200));
        Assert.Equal(1, ScreenTimeRules.Share(9000, 7200));
        Assert.Equal(0, ScreenTimeRules.Share(3600, 0));
        Assert.Equal(0, ScreenTimeRules.Share(-60, 7200));
    }

    [Fact]
    public void S453_switches_add_up_across_the_window()
    {
        Assert.Equal(7, ScreenTimeRules.TotalSwitches([Day("a") with { Switches = 3 }, Day("b") with { Switches = 4 }]));
        Assert.Equal(0, ScreenTimeRules.TotalSwitches([]));
    }

    // ---- [S-457] limits ---------------------------------------------------------------------------

    [Theory]
    [InlineData(4 * 3600, "4h")]
    [InlineData((4 * 3600) + (15 * 60), "4h 15m")]
    [InlineData(45 * 60, "45m")]
    public void S457_limits_read_as_whole_hours(long seconds, string expected)
        => Assert.Equal(expected, ScreenTimeRules.FormatLimit(seconds));

    [Fact]
    public void S457_a_limit_is_a_positive_number_of_seconds()
    {
        Assert.Equal(3600, ScreenTimeRules.Limit(3600));
        Assert.Null(ScreenTimeRules.Limit(0));
        Assert.Null(ScreenTimeRules.Limit(-60));
    }

    [Fact]
    public void S3322_preferences_drop_entries_without_a_key_or_a_limit()
    {
        var preferences = ScreenTimePreferences.From(
            ["a", string.Empty, " ", " b ", "a"],
            -60,
            [new("chrome", 3600), new("code", 0), new(" ", 60), new("spotify", -5)]);

        Assert.Equal(["a", "b"], preferences.Hidden.Order());
        Assert.Equal(0, preferences.GoalSeconds);
        Assert.Equal(["chrome"], preferences.AppLimits.Keys);
        Assert.True(ScreenTimePreferences.From(null, 0, null).IsEmpty);
    }

    [Fact]
    public void S3322_a_copy_is_its_own()
    {
        var original = ScreenTimePreferences.From(["a"], 3600, [new("chrome", 60)]);
        var copy = original.Copy();
        copy.Hidden.Add("b");
        copy.AppLimits["code"] = 120;

        Assert.Equal(["a"], original.Hidden);
        Assert.Equal(["chrome"], original.AppLimits.Keys);
        Assert.Equal(3600, copy.GoalSeconds);
    }

    // ---- [S-3310] [S-3312] the linked focus -------------------------------------------------------------

    [Fact]
    public void S3310_the_hover_wins_over_the_selection()
    {
        Assert.Equal("code", ScreenTimeRules.Focus("code", "chrome"));
        Assert.Equal("chrome", ScreenTimeRules.Focus(null, "chrome"));
        Assert.Null(ScreenTimeRules.Focus(null, null));
    }

    [Fact]
    public void S3312_an_app_folded_into_other_lights_other()
    {
        ScreenTimeApp[] apps = [App("code", 100), App("chrome", 60), App("slack", 20)];
        var parts = ScreenTimeRules.Segments(apps, 180, 1, "Other");

        Assert.Equal("code", ScreenTimeRules.FocusPart("code", parts, apps));
        Assert.Equal(ScreenTimeRules.OtherKey, ScreenTimeRules.FocusPart("slack", parts, apps));
        Assert.Equal(ScreenTimeRules.OtherKey, ScreenTimeRules.FocusPart(ScreenTimeRules.OtherKey, parts, apps));
        Assert.Null(ScreenTimeRules.FocusPart(null, parts, apps));

        // Hidden, or not used in this period: nothing lights, not even Other.
        Assert.Null(ScreenTimeRules.FocusPart("spotify", parts, apps));
        var all = ScreenTimeRules.Segments([App("code", 100)], 100, 7, "Other");
        Assert.Null(ScreenTimeRules.FocusPart("slack", all, [App("code", 100)]));
    }

    [Fact]
    public void S3312_the_focused_part_is_lit_and_the_rest_dim()
    {
        Assert.Equal(ScreenTimeEmphasis.Normal, ScreenTimeRules.Emphasis("code", null));
        Assert.Equal(ScreenTimeEmphasis.Lit, ScreenTimeRules.Emphasis("code", "code"));
        Assert.Equal(ScreenTimeEmphasis.Dimmed, ScreenTimeRules.Emphasis("chrome", "code"));
    }

    [Fact]
    public void S457_goal_state_reports_ratio_and_over()
    {
        Assert.Equal(new ScreenTimeGoal(3600, 1800, 0.5, false), ScreenTimeRules.Goal(1800, 3600));
        Assert.Equal(new ScreenTimeGoal(3600, 3600, 1, false), ScreenTimeRules.Goal(3600, 3600));
        Assert.True(ScreenTimeRules.Goal(5040, 3600)!.Value.Over);
        Assert.Equal(0, ScreenTimeRules.Goal(-60, 3600)!.Value.Used);
        Assert.Null(ScreenTimeRules.Goal(3600, null));
    }

    [Fact]
    public void S457_a_window_judges_its_daily_average()
    {
        // 14h over a week is 2h a day: under a 3h daily limit although the total is far above it.
        Assert.False(ScreenTimeRules.Goal(ScreenTimeRules.DailyAverage(14 * 3600, 7), 3 * 3600)!.Value.Over);
        Assert.True(ScreenTimeRules.Goal(14 * 3600, 3 * 3600)!.Value.Over);
    }

    // ---- [S-454] the day navigation --------------------------------------------------------------

    [Fact]
    public void S454_a_pinned_day_is_described_against_today()
    {
        var today = new LocalDay(new DateOnly(2026, 7, 2));

        Assert.Equal(ScreenTimeDayKind.Today, ScreenTimeRules.Describe(null, today));
        Assert.Equal(ScreenTimeDayKind.Yesterday, ScreenTimeRules.Describe("2026-07-01", today));
        Assert.Equal(ScreenTimeDayKind.Date, ScreenTimeRules.Describe("2026-06-30", today));
        Assert.Equal(ScreenTimeDayKind.Today, ScreenTimeRules.Describe("2026-07-02", today));
        Assert.Equal(ScreenTimeDayKind.Today, ScreenTimeRules.Describe("garbage", today));

        // After midnight a pinned "yesterday" becomes a plain date.
        Assert.Equal(ScreenTimeDayKind.Date, ScreenTimeRules.Describe("2026-07-01", today.AddDays(1)));
    }

    [Fact]
    public void S454_the_navigation_reaches_ninety_calendar_days()
        => Assert.Equal(89, ScreenTimeRules.MaxHistoryOffset);

    [Fact]
    public void S454_stepping_pins_a_date_and_returning_to_today_unpins()
    {
        var today = new LocalDay(new DateOnly(2026, 7, 2));

        Assert.Equal("2026-07-01", ScreenTimeRules.Step(null, today, -1));
        Assert.Equal("2026-06-30", ScreenTimeRules.Step("2026-07-01", today, -1));
        Assert.Null(ScreenTimeRules.Step("2026-07-01", today, 1));

        // Never past today, never further back than the window.
        Assert.Null(ScreenTimeRules.Step(null, today, 5));
        Assert.Equal(today.AddDays(-89).Key, ScreenTimeRules.Step(today.AddDays(-89).Key, today, -1));
        Assert.False(ScreenTimeRules.CanGoBack(today.AddDays(-89).Key, today));
        Assert.True(ScreenTimeRules.CanGoBack(today.AddDays(-88).Key, today));
        Assert.True(ScreenTimeRules.CanGoBack(null, today));
    }

    [Fact]
    public void S454_a_pinned_date_survives_midnight()
    {
        // Pinned to the 1st on the 2nd; at midnight today becomes the 3rd and the pin stays the 1st.
        var pinned = ScreenTimeRules.Step(null, new LocalDay(new DateOnly(2026, 7, 2)), -1);
        Assert.Equal("2026-07-01", pinned);
        Assert.Equal("2026-06-30", ScreenTimeRules.Step(pinned, new LocalDay(new DateOnly(2026, 7, 3)), -1));
    }

    [Fact]
    public void S1210_durations_are_worded_per_language()
    {
        Assert.Equal("3 h 24 min", ScreenTimeRules.FormatDuration(Locale.De, (3 * 3600) + (24 * 60)));
        Assert.Equal("4,6 h", ScreenTimeRules.CompactDuration(Locale.De, (4 * 3600) + (37 * 60)));
        Assert.Equal("4 h", ScreenTimeRules.FormatLimit(Locale.De, 4 * 3600));
        Assert.Equal("<1m", ScreenTimeRules.FormatDuration(Locale.En, 5));
    }

    [Fact]
    public void S454_labels()
    {
        var today = new LocalDay(new DateOnly(2026, 7, 2));

        Assert.Equal("Today", ScreenTimeRules.DayLabel(null, today));
        Assert.Equal("Yesterday", ScreenTimeRules.DayLabel("2026-07-01", today));
        Assert.Equal("Tue, Jun 30", ScreenTimeRules.DayLabel(Locale.En, "2026-06-30", today));
        Assert.Equal("Gestern", ScreenTimeRules.DayLabel(Locale.De, "2026-07-01", today));
        Assert.Equal("Last 7 days", ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Week));
        Assert.Equal("Last 30 days", ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month));
    }
}
