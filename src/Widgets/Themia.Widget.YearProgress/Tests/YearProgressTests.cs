using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.YearProgress.Tests;

/// <summary>
/// [S-4800]..[S-4805], [S-6040]..[S-6042] Year progress: its config, the spans and their fractions,
/// the week, the bars' ladder and the rings' and dots' styles. Moved with the widget from Core's
/// DeskInfoWidgetsTests and TimeStylesTests, unchanged in what they assert.
/// </summary>
public sealed class YearProgressTests
{
    /// <summary>A Central European zone: +1, +2 from the last Sunday of March 02:00 to the last Sunday of October 03:00.</summary>
    private static readonly TimeZoneInfo Cet = TimeZoneInfo.CreateCustomTimeZone(
        "Test/CET",
        TimeSpan.FromHours(1),
        "CET",
        "CET",
        "CEST",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date,
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday)),
        ]);

    private static DateTimeOffset Local(int year, int month, int day, int hour = 0, int minute = 0)
    {
        var clock = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(clock, Cet.GetUtcOffset(clock));
    }

    public static TheoryData<string> Tiers() => [.. WidgetTiers.All.Select(t => t.Name)];

    /// <summary>A tier's area less the 16 DIP inset all round, as the view deflates it.</summary>
    private static WidgetArea Inset(string tier)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        return new WidgetArea(t.Width - 32, t.Height - 32);
    }

    // -----------------------------------------------------------------------------------------
    // [S-4800] Its config
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S4800_its_kind_has_its_config_and_round_trips()
    {
        var kind = new WidgetId("yearProgress");
        var config = WidgetConfig.CreateDefault(kind);
        Assert.IsType<YearProgressConfig>(config);
        Assert.Equal(kind, config.Kind);

        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
        Assert.IsType<YearProgressConfig>(back);
        Assert.Empty(back!.Repair());
    }

    [Fact]
    public void S4800_it_never_needs_setting_up()
        => Assert.False(WidgetSetup.Needed(new YearProgressConfig(), FeatureSet.None));

    [Fact]
    public void S6040_it_offers_its_styles_in_order_the_old_look_first()
        => Assert.Equal("bars,rings,dots", string.Join(",", WidgetStyles.For(new WidgetId("yearProgress")).Select(s => s.Name)));

    // -----------------------------------------------------------------------------------------
    // [S-4801] Fractions
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S4801_a_spring_forward_day_is_23_hours_and_half_gone_at_half_past_twelve()
    {
        var day = TimeProgress.Of(ProgressSpan.Day, Local(2026, 3, 29, 12, 30), Cet, WeekStartDay.Monday);

        Assert.Equal(TimeSpan.FromHours(23), day.Length);
        Assert.Equal(0.5, day.Fraction, 6);
    }

    [Fact]
    public void S4801_a_fall_back_day_is_25_hours()
    {
        var day = TimeProgress.Of(ProgressSpan.Day, Local(2026, 10, 25, 12, 0), Cet, WeekStartDay.Monday);

        Assert.Equal(TimeSpan.FromHours(25), day.Length);
        Assert.Equal(13.0 / 25, day.Fraction, 6);
    }

    [Fact]
    public void S4801_a_leap_year_has_366_days_and_is_half_gone_at_noon_on_2_july()
    {
        var year = TimeProgress.Of(ProgressSpan.Year, new DateTimeOffset(2028, 7, 2, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        Assert.Equal(366, year.Days);
        Assert.Equal(183.0 / 366, year.Fraction, 6);
        Assert.Equal(184, year.DayOf(new DateOnly(2028, 7, 2)));

        var common = TimeProgress.Of(ProgressSpan.Year, new DateTimeOffset(2027, 7, 2, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        Assert.Equal(365, common.Days);
    }

    [Fact]
    public void S4801_february_of_a_leap_year_has_29_days()
    {
        var month = TimeProgress.Of(ProgressSpan.Month, new DateTimeOffset(2028, 2, 15, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        Assert.Equal(29, month.Days);
        Assert.Equal(new DateOnly(2028, 2, 1), month.First);
        Assert.Equal(14.0 / 29, month.Fraction, 6);
    }

    [Fact]
    public void S4801_the_spans_are_read_in_the_zone_not_in_utc()
    {
        // 23:30 UTC on 31 December is already New Year in Central Europe.
        var year = TimeProgress.Of(ProgressSpan.Year, new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero), Cet, WeekStartDay.Monday);
        Assert.Equal(new DateOnly(2027, 1, 1), year.First);
        Assert.True(year.Fraction < 0.001);
    }

    [Fact]
    public void S4801_percent_floors_so_a_hundred_is_only_the_end()
    {
        Assert.Equal(99, TimeProgress.Percent(0.99999));
        Assert.Equal(100, TimeProgress.Percent(1));
        Assert.Equal(78.4, TimeProgress.Percent(0.78449, 1));
        Assert.Equal(0, TimeProgress.Percent(-0.2));
    }

    // -----------------------------------------------------------------------------------------
    // [S-4802] Week start and week numbers
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S4802_the_week_starts_on_the_regional_first_day()
    {
        var wednesday = new DateOnly(2026, 10, 14);
        Assert.Equal(new DateOnly(2026, 10, 12), TimeProgress.WeekStart(wednesday, WeekStartDay.Monday));
        Assert.Equal(new DateOnly(2026, 10, 11), TimeProgress.WeekStart(wednesday, WeekStartDay.Sunday));
        Assert.Equal(new DateOnly(2026, 10, 10), TimeProgress.WeekStart(wednesday, WeekStartDay.Saturday));

        // Sunday: the last day of a Monday week, the first of a Sunday week.
        var sunday = new DateOnly(2026, 10, 18);
        Assert.Equal(new DateOnly(2026, 10, 12), TimeProgress.WeekStart(sunday, WeekStartDay.Monday));
        Assert.Equal(sunday, TimeProgress.WeekStart(sunday, WeekStartDay.Sunday));

        var monday = TimeProgress.Of(ProgressSpan.Week, new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Monday);
        var sundayWeek = TimeProgress.Of(ProgressSpan.Week, new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, WeekStartDay.Sunday);
        Assert.Equal(6.5 / 7, monday.Fraction, 6);
        Assert.Equal(0.5 / 7, sundayWeek.Fraction, 6);
    }

    [Theory]
    [InlineData(2026, 12, 31, WeekStartDay.Monday, 53)]
    [InlineData(2027, 1, 1, WeekStartDay.Monday, 53)]
    [InlineData(2024, 12, 30, WeekStartDay.Monday, 1)]
    [InlineData(2026, 10, 12, WeekStartDay.Monday, 42)]
    [InlineData(2026, 1, 1, WeekStartDay.Sunday, 1)]
    [InlineData(2026, 1, 3, WeekStartDay.Sunday, 1)]
    [InlineData(2026, 1, 4, WeekStartDay.Sunday, 2)]
    [InlineData(2026, 12, 31, WeekStartDay.Sunday, 53)]
    [InlineData(2026, 1, 2, WeekStartDay.Saturday, 1)]
    [InlineData(2026, 1, 3, WeekStartDay.Saturday, 2)]
    public void S4802_week_numbers_are_iso_for_monday_and_us_for_sunday(int year, int month, int day, WeekStartDay start, int expected)
        => Assert.Equal(expected, TimeProgress.WeekNumber(new DateOnly(year, month, day), start));

    // -----------------------------------------------------------------------------------------
    // [S-4803] The ladder
    // -----------------------------------------------------------------------------------------

    private static WidgetArea InsetBy(double width, double height) => new WidgetArea(width, height).Deflate(16, 16);

    [Fact]
    public void S4803_the_smallest_tile_shows_the_year_alone()
    {
        var plan = YearProgressLayout.Plan(InsetBy(120, 76));
        Assert.True(plan.Hero);
        Assert.Equal([ProgressSpan.Year], plan.Rows);
    }

    [Fact]
    public void S4803_a_short_tile_keeps_the_year_then_the_day_in_list_order()
    {
        var plan = YearProgressLayout.Plan(InsetBy(200, 106));
        Assert.False(plan.Hero);
        Assert.Equal([ProgressSpan.Day, ProgressSpan.Year], plan.Rows);
    }

    [Fact]
    public void S4803_a_medium_tile_shows_all_four_and_a_large_one_adds_details()
    {
        var medium = YearProgressLayout.Plan(InsetBy(320, 196));
        Assert.Equal(TimeProgress.All, medium.Rows);
        Assert.False(medium.Details);

        var large = YearProgressLayout.Plan(InsetBy(560, 376));
        Assert.Equal(TimeProgress.All, large.Rows);
        Assert.True(large.Details);
    }

    [Fact]
    public void S4803_a_tall_tile_puts_the_year_large_over_the_rows()
    {
        Assert.True(YearProgressLayout.Plan(InsetBy(200, 356)).Headline);
        Assert.False(YearProgressLayout.Plan(InsetBy(320, 196)).Headline);
    }

    [Fact]
    public void S4803_a_strip_stands_the_four_side_by_side()
    {
        var plan = YearProgressLayout.Plan(InsetBy(480, 116));
        Assert.True(plan.Columns);
        Assert.Equal(TimeProgress.All, plan.Rows);
    }

    [Fact]
    public void S4803_growing_a_tile_never_drops_a_row()
    {
        var previous = 0;
        for (var height = 40; height <= 600; height += 4)
        {
            var count = YearProgressLayout.Plan(new WidgetArea(260, height)).Rows.Count;
            Assert.True(count >= previous, $"height {height}: {count} rows after {previous}");
            previous = count;
        }
    }

    // ---- year progress ------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S6041_rings_fit_every_tier_and_a_larger_tile_draws_larger_rings_not_more(string tier)
    {
        var area = Inset(tier);
        var plan = YearStyleLayout.Rings(area);

        Assert.InRange(plan.Spans.Count, 1, 4);
        Assert.Contains(ProgressSpan.Year, plan.Spans);
        var rows = (plan.Spans.Count + plan.Columns - 1) / plan.Columns;
        var label = plan.Labels ? FluentType.Caption.LineHeight + YearProgressLayout.BarGap : 0;
        Assert.True((plan.Columns * plan.Diameter) + ((plan.Columns - 1) * YearStyleLayout.RingGap) <= area.Width + 1e-6, tier);
        Assert.True((rows * (plan.Diameter + label)) + ((rows - 1) * YearStyleLayout.RingGap) <= area.Height + 1e-6, tier);
    }

    [Fact]
    public void S6041_from_m_up_all_four_rings_show_and_grow_with_the_tile()
    {
        var m = YearStyleLayout.Rings(Inset("M"));
        var xl = YearStyleLayout.Rings(Inset("XL"));
        Assert.Equal(TimeProgress.All, m.Spans);
        Assert.Equal(TimeProgress.All, xl.Spans);
        Assert.True(xl.Diameter > m.Diameter);
        Assert.Equal(4, YearStyleLayout.Rings(Inset("Wide")).Columns);
        Assert.Equal([ProgressSpan.Year], YearStyleLayout.Rings(Inset("XS")).Spans);
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S6042_a_dot_a_day_fits_every_tier_with_the_caption_only_where_the_grid_keeps_its_pitch(string tier)
    {
        var area = Inset(tier);

        foreach (var days in new[] { 365, 366 })
        {
            var plan = YearStyleLayout.Dots(area, days);
            Assert.False(plan.Hero, tier);
            Assert.True(plan.Grid.Columns * plan.Grid.Rows >= days, tier);
            var room = area.Height - (plan.Caption ? FluentType.Caption.LineHeight + YearStyleLayout.CaptionGap : 0);
            Assert.True(plan.Grid.Height <= room + 1e-6 && plan.Grid.Width <= area.Width + 1e-6, tier);
            Assert.True(!plan.Caption || plan.Grid.Pitch >= YearStyleLayout.CaptionPitch, tier);
        }

        Assert.True(YearStyleLayout.Dots(new WidgetArea(30, 20), 365).Hero);
    }
}
