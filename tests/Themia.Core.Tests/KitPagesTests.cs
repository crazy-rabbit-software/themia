using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-7035] [S-7036] The kit's pages with day groups and the schedule look (the agenda's rules,
/// [S-2709], [S-2716], [S-2717], [S-1267]; the calendar's own tests of them are in
/// <c>CalendarFitTests</c> and <c>CalendarDaysTests</c>).
/// </summary>
public sealed class KitPagesTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static IReadOnlyList<string> Keys(params int[] perDay)
        => [.. perDay.SelectMany((n, d) => Enumerable.Repeat(Today.AddDays(d).ToString("O", System.Globalization.CultureInfo.InvariantCulture), n))];

    [Fact]
    public void S7035_a_page_always_moves_and_a_column_starting_mid_day_repeats_its_heading()
    {
        var keys = Keys(5);
        Assert.Equal(1, KitPages.Fit(keys, 0, 10, 32, 44, firstHeading: true));
        Assert.True(KitPages.HeadingBefore(keys, 3, 3, firstHeading: true));
        Assert.False(KitPages.HeadingBefore(keys, 0, 3, firstHeading: true));
        Assert.Equal([0, 2, 4], KitPages.Pages(keys, 1, 120, 32, 44, firstHeading: true));
        Assert.Equal(1, KitPages.PageOf([0, 2, 4], 3));
        Assert.Equal(0, KitPages.PageOf([0, 2, 4], 0));
    }

    [Fact]
    public void S7035_a_heading_comes_over_every_new_group_and_two_columns_fill_one_after_the_other()
    {
        var keys = Keys(1, 2, 1);
        Assert.True(KitPages.HeadingBefore(keys, 0, 1, firstHeading: false));
        Assert.False(KitPages.HeadingBefore(keys, 0, 2, firstHeading: false));
        Assert.Equal(32 + 44 + 32 + 44 + 44, KitPages.ColumnHeight(keys, 0, 3, 32, 44, firstHeading: true));
        Assert.Equal([0], KitPages.Pages(keys, 2, 200, 32, 44, firstHeading: true));
        Assert.Equal([0, 3], KitPages.Pages(keys, 1, 200, 32, 44, firstHeading: true));
    }

    private static readonly KitSpec Schedule = new(KitRowLook.Schedule, KitShow.None, () => string.Empty, () => string.Empty, Overflow: KitOverflow.Page);

    [Theory]
    [InlineData(120, 76, 1, false, true)]
    [InlineData(320, 196, 1, false, false)]
    [InlineData(560, 376, 1, true, false)]
    [InlineData(800, 516, 2, false, false)]
    [InlineData(1400, 360, 2, true, false)]
    [InlineData(200, 356, 1, false, true)]
    public void S7036_the_schedule_plan_follows_the_width(double width, double height, int columns, bool details, bool narrow)
    {
        var plan = KitLadder.Plan(new WidgetArea(width, height), Schedule, KitShow.None);

        Assert.Equal(columns, plan.Columns);
        Assert.Equal(columns == 1 ? width : (width - KitLadder.ScheduleGutter) / 2, plan.ColumnWidth);
        Assert.Equal(details, plan.Details);
        Assert.Equal(narrow, plan.Narrow);
        Assert.Equal(height < KitLadder.CompactBelow, plan.Compact);
        Assert.Equal(FluentType.BodyStrong.LineHeight + 12, plan.HeadingHeight);
    }

    [Fact]
    public void S7036_rows_have_two_lines_unless_the_tile_is_compact_and_wide()
    {
        var compactWide = KitLadder.Plan(new WidgetArea(480, 116), Schedule, KitShow.None);
        var compactNarrow = KitLadder.Plan(new WidgetArea(200, 106), Schedule, KitShow.None);
        var roomy = KitLadder.Plan(new WidgetArea(320, 300), Schedule, KitShow.None);

        Assert.Equal(Math.Max(32, FluentType.BodyStrong.LineHeight + 8), compactWide.RowHeight);
        Assert.Equal(FluentType.BodyStrong.LineHeight + FluentType.Caption.LineHeight + 8, compactNarrow.RowHeight);
        Assert.Equal(compactNarrow.RowHeight, roomy.RowHeight);
    }

    [Fact]
    public void S2716_other_days_are_their_date_and_today_its_word()
    {
        Assert.Equal(LocaleFormat.Date(Locale.En, Today.AddDays(5), DateStyle.WeekdayMonthDayShort), KitDays.Heading(Locale.En, Today.AddDays(5), Today, dated: true));
        Assert.Equal("Today", KitDays.Heading(Locale.En, Today, Today, dated: false));
    }
}
