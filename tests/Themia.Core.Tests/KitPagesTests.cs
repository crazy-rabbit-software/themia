using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-7035] [S-7036] The kit's pages with day groups and the schedule look: the agenda's rules
/// ([S-2709], [S-2716], [S-2717], [S-1267]) as the kit's, proved equal to the calendar's own before
/// those go.
/// </summary>
public sealed class KitPagesTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static IReadOnlyList<DateOnly> Days(params int[] perDay)
        => [.. perDay.SelectMany((n, d) => Enumerable.Repeat(Today.AddDays(d), n))];

    private static IReadOnlyList<string> Keys(IReadOnlyList<DateOnly> days) => [.. days.Select(d => d.ToString("O"))];

    public static TheoryData<int[]> Shapes => new()
    {
        new[] { 1 },
        new[] { 3, 1, 4 },
        new[] { 6, 6, 6, 6 },
        new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        new[] { 12 },
        new[] { 2, 9, 1, 3 },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void S7035_the_kits_pages_are_the_agendas(int[] perDay)
    {
        var days = Days(perDay);
        var keys = Keys(days);

        foreach (var columns in new[] { 1, 2 })
        {
            foreach (var height in new[] { 40.0, 76, 106, 128, 196, 356, 516 })
            {
                foreach (var first in new[] { true, false })
                {
                    Assert.Equal(CalendarFit.AgendaPages(days, columns, height, 32, 44, first), KitPages.Pages(keys, columns, height, 32, 44, first));

                    for (var start = 0; start < days.Count; start++)
                    {
                        var fit = KitPages.Fit(keys, start, height, 32, 44, first);
                        Assert.Equal(CalendarFit.AgendaFit(days, start, height, 32, 44, first), fit);
                        Assert.Equal(CalendarFit.AgendaColumnHeight(days, start, fit, 32, 44, first), KitPages.ColumnHeight(keys, start, fit, 32, 44, first));
                        Assert.Equal(CalendarFit.AgendaHeadingBefore(days, 0, start, first), KitPages.HeadingBefore(keys, 0, start, first));
                    }
                }
            }
        }
    }

    [Fact]
    public void S7035_a_page_always_moves_and_a_column_starting_mid_day_repeats_its_heading()
    {
        var keys = Keys(Days(5));
        Assert.Equal(1, KitPages.Fit(keys, 0, 10, 32, 44, firstHeading: true));
        Assert.True(KitPages.HeadingBefore(keys, 3, 3, firstHeading: true));
        Assert.False(KitPages.HeadingBefore(keys, 0, 3, firstHeading: true));
        Assert.Equal([0, 2, 4], KitPages.Pages(keys, 1, 120, 32, 44, firstHeading: true));
        Assert.Equal(1, KitPages.PageOf([0, 2, 4], 3));
        Assert.Equal(0, KitPages.PageOf([0, 2, 4], 0));
    }

    [Fact]
    public void S7035_the_first_heading_and_second_lines_are_the_agendas()
    {
        foreach (var height in new[] { 0.0, 100, 127, 128, 300 })
        {
            Assert.Equal(CalendarFit.ShowsFirstHeading(Today, Today, height), KitPages.ShowsFirstHeading(Today, Today, height));
            Assert.Equal(CalendarFit.ShowsFirstHeading(Today.AddDays(1), Today, height), KitPages.ShowsFirstHeading(Today.AddDays(1), Today, height));
        }

        bool[] wraps = [true, false, true, true];
        Assert.Equal(CalendarFit.SecondLines(wraps, 40, 20), Disclosure.SecondLines(wraps, 40, 20));
        Assert.Equal([true, false, true, false], Disclosure.SecondLines(wraps, 40, 20));
    }

    [Fact]
    public void S2716_day_headings_are_the_agendas()
    {
        foreach (var locale in new[] { Locale.En, Locale.De, Locale.Ja })
        {
            foreach (var offset in new[] { 0, 1, 2, 9 })
            {
                foreach (var dated in new[] { true, false })
                {
                    Assert.Equal(CalendarFit.AgendaHeading(locale, Today.AddDays(offset), Today, dated), KitDays.Heading(locale, Today.AddDays(offset), Today, dated));
                }
            }

            foreach (var width in new[] { 80.0, 160, 240, 400 })
            {
                Assert.Equal(CalendarFit.AgendaHeadingDated(locale, width), KitDays.Dated(locale, width));
            }
        }
    }

    private static readonly KitSpec Schedule = new(KitRowLook.Schedule, KitShow.None, () => string.Empty, () => string.Empty, Overflow: KitOverflow.Page);

    [Theory]
    [InlineData(120, 76)]
    [InlineData(200, 106)]
    [InlineData(320, 196)]
    [InlineData(480, 116)]
    [InlineData(200, 356)]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    [InlineData(1400, 360)]
    public void S7036_the_schedule_plan_is_the_agendas(double width, double height)
    {
        var plan = KitLadder.Plan(new WidgetArea(width, height), Schedule, KitShow.None);

        var columns = width >= CalendarFit.AgendaTwoColumnsFrom ? 2 : 1;
        var columnWidth = columns == 1 ? width : (width - 8) / 2;
        var compact = height > 0 && height < 200;
        var narrow = columnWidth > 0 && columnWidth < 260;

        Assert.Equal(columns, plan.Columns);
        Assert.Equal(columnWidth, plan.ColumnWidth);
        Assert.Equal(compact, plan.Compact);
        Assert.Equal(narrow, plan.Narrow);
        Assert.Equal(columnWidth >= CalendarFit.AgendaColumnsFrom && !compact, plan.Details);
        Assert.Equal(narrow || !compact
            ? FluentType.BodyStrong.LineHeight + FluentType.Caption.LineHeight + 8
            : Math.Max(32, FluentType.BodyStrong.LineHeight + 8), plan.RowHeight);
        Assert.Equal(FluentType.BodyStrong.LineHeight + 12, plan.HeadingHeight);

        foreach (var label in new[] { 20.0, 52, 70, 133, 300 })
        {
            Assert.Equal(CalendarFit.AgendaTimeColumn(label, columnWidth), KitLadder.TimeColumn(label, columnWidth));
        }
    }
}
