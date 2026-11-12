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

    [Fact]
    public void S2280_rows_in_no_group_have_no_heading_and_fill_the_column()
    {
        IReadOnlyList<string> keys = ["", "", "", ""];
        Assert.False(KitPages.HeadingBefore(keys, 0, 0, firstHeading: true));
        Assert.False(KitPages.HeadingBefore(keys, 2, 2, firstHeading: true));
        Assert.Equal(3, KitPages.Fit(keys, 0, 140, 32, 44, firstHeading: true));
        Assert.Equal(3 * 44, KitPages.ColumnHeight(keys, 0, 3, 32, 44, firstHeading: true));
        Assert.Equal([0, 3], KitPages.Pages(keys, 1, 140, 32, 44, firstHeading: true));
    }

    private static readonly KitSpec Schedule = new(KitRowLook.Schedule, KitShow.None, () => string.Empty, () => string.Empty, Overflow: KitOverflow.Page);

    [Theory]
    [InlineData(120, 76, 1, 1)]
    [InlineData(320, 196, 1, 2)]
    [InlineData(480, 116, 1, 1)]
    [InlineData(560, 376, 1, 2)]
    [InlineData(800, 516, 2, 2)]
    [InlineData(200, 356, 1, 2)]
    public void S7036_the_schedule_plan_follows_the_width_and_the_tier(double width, double height, int columns, int lines)
    {
        var plan = KitLadder.Plan(new WidgetArea(width, height), Schedule, KitShow.None);

        Assert.Equal(columns, plan.Columns);
        Assert.Equal(width / columns, plan.ColumnWidth);
        Assert.Equal(lines, plan.Lines);
        Assert.Equal(FamilyShape.RowHeight(lines), plan.RowHeight);
        Assert.Equal(FamilyShape.Row1, plan.HeadingHeight);
    }

    [Fact]
    public void S2716_other_days_are_their_date_and_today_its_word()
    {
        Assert.Equal(LocaleFormat.Date(Locale.En, Today.AddDays(5), DateStyle.WeekdayMonthDayShort), KitDays.Heading(Locale.En, Today.AddDays(5), Today, dated: true));
        Assert.Equal("Today", KitDays.Heading(Locale.En, Today, Today, dated: false));
    }
}
