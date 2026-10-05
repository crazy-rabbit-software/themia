using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-660..S-665] The shared responsive-layout foundation: size classes, the disclosure ladder, whole rows.</summary>
public class ResponsiveLayoutTests
{
    /// <summary>The card's header takes about this much of the tier cards' heights.</summary>
    private const double Header = 44;

    [Theory]
    [InlineData(120, 120, SizeClass.Compact, SizeClass.Compact)]    // XS
    [InlineData(200, 150, SizeClass.Regular, SizeClass.Compact)]    // S
    [InlineData(320, 240, SizeClass.Regular, SizeClass.Regular)]    // M
    [InlineData(480, 160, SizeClass.Expanded, SizeClass.Compact)]   // Wide
    [InlineData(200, 400, SizeClass.Regular, SizeClass.Expanded)]   // Tall
    [InlineData(560, 420, SizeClass.Expanded, SizeClass.Expanded)]  // L
    [InlineData(800, 560, SizeClass.Huge, SizeClass.Huge)]          // XL
    public void S660_each_size_tier_lands_in_its_own_class_pair(double cardWidth, double cardHeight, SizeClass width, SizeClass height)
    {
        var area = new WidgetArea(cardWidth, cardHeight - Header);

        Assert.Equal(width, area.WidthClass);
        Assert.Equal(height, area.HeightClass);
    }

    [Fact]
    public void S660_the_seven_tiers_are_seven_distinct_pairs()
    {
        (double W, double H)[] tiers = [(120, 120), (200, 150), (320, 240), (480, 160), (200, 400), (560, 420), (800, 560)];

        var pairs = tiers.Select(t => new WidgetArea(t.W, t.H - Header)).Select(a => (a.WidthClass, a.HeightClass)).Distinct();

        Assert.Equal(tiers.Length, pairs.Count());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.PositiveInfinity)]
    [InlineData(-5, 300)]
    public void S660_an_unmeasured_area_is_compact_and_says_so(double width, double height)
    {
        var area = new WidgetArea(width, height);

        Assert.False(area.IsMeasured);
        Assert.Equal(SizeClass.Compact, area.Least);
        Assert.Equal(AreaShape.Square, area.Shape);
    }

    [Fact]
    public void S660_a_breakpoint_is_the_first_length_of_the_next_class()
    {
        Assert.Equal(SizeClass.Compact, WidgetArea.Classify(179.9, WidgetArea.WidthBreakpoints));
        Assert.Equal(SizeClass.Regular, WidgetArea.Classify(180, WidgetArea.WidthBreakpoints));
        Assert.Equal(SizeClass.Huge, WidgetArea.Classify(10_000, WidgetArea.WidthBreakpoints));
    }

    [Theory]
    [InlineData(480, 116, AreaShape.Wide)]
    [InlineData(200, 356, AreaShape.Tall)]
    [InlineData(320, 196, AreaShape.Square)]
    [InlineData(560, 376, AreaShape.Square)]
    public void S660_shape_follows_the_proportion(double width, double height, AreaShape shape)
        => Assert.Equal(shape, new WidgetArea(width, height).Shape);

    [Fact]
    public void S660_deflate_never_goes_negative()
    {
        var inner = new WidgetArea(20, 10).Deflate(16, 8);

        Assert.Equal(0, inner.Width);
        Assert.Equal(0, inner.Height);
    }

    [Fact]
    public void S661_parts_are_shown_in_order_while_they_fit()
    {
        var room = new Disclosure(200, 100);

        Assert.True(room.Take(40));
        Assert.True(room.Take(40));
        Assert.False(room.Take(40));
        Assert.Equal(20, room.Remaining);
    }

    [Fact]
    public void S661_once_a_part_misses_no_later_part_is_shown_even_a_smaller_one()
    {
        // The ladder rule: growing a tile only ever adds parts, in order.
        var room = new Disclosure(200, 100);

        Assert.True(room.Take(60));
        Assert.False(room.Take(50));
        Assert.False(room.Take(10));
        Assert.Equal(0, room.TakeRows(5, 10));
    }

    [Fact]
    public void S661_a_part_too_narrow_is_skipped_without_closing_the_ladder()
    {
        var room = new Disclosure(150, 100);

        Assert.False(room.Take(20, minWidth: 240));
        Assert.True(room.Take(20));
        Assert.Equal(80, room.Remaining);
    }

    [Fact]
    public void S661_the_gap_is_paid_between_parts_not_before_the_first()
    {
        var room = new Disclosure(200, 100, gap: 8);

        room.Reserve(28);
        Assert.True(room.Take(24));   // 28 + 8 + 24 = 60
        Assert.True(room.Take(32));   // + 8 + 32 = 100
        Assert.False(room.Take(1));
        Assert.Equal(0, room.Remaining);
    }

    [Fact]
    public void S661_a_part_short_by_a_rounding_fraction_still_fits()
        => Assert.True(new Disclosure(100, 39.6).Take(40));

    [Fact]
    public void S661_reserve_always_spends_and_reports_whether_it_fitted()
    {
        var room = new Disclosure(100, 30);

        Assert.False(room.Reserve(40));
        Assert.Equal(0, room.Remaining);
        Assert.False(room.Take(1));
    }

    [Fact]
    public void S661_an_unmeasured_budget_shows_no_optional_part()
    {
        var room = new Disclosure(new WidgetArea(0, 0));

        Assert.False(room.Take(1));
        Assert.Equal(0, room.TakeRows(10, 5));
    }

    [Fact]
    public void S661_disclosure_is_monotonic_as_the_tile_grows()
    {
        // A part shown at one height is shown at every taller height.
        double[] parts = [16, 24, 16, 40];
        var shownBefore = 0;

        for (var height = 0; height <= 200; height++)
        {
            var room = new Disclosure(300, height, gap: 8);
            room.Reserve(28);
            var shown = parts.TakeWhile(p => room.Take(p)).Count();

            Assert.True(shown >= shownBefore, $"at {height} showed {shown}, below {shownBefore}");
            shownBefore = shown;
        }
    }

    [Theory]
    [InlineData(100, 20, 0, 5)]
    [InlineData(99, 20, 0, 4)]
    [InlineData(99.6, 20, 0, 5)]    // rounding tolerance
    [InlineData(19, 20, 0, 0)]
    [InlineData(100, 20, 5, 4)]     // 4 rows + 3 gaps = 95
    [InlineData(115, 20, 5, 4)]     // 5 rows + 4 gaps = 120 > 115
    [InlineData(120, 20, 5, 5)]
    [InlineData(0, 20, 0, 0)]
    [InlineData(100, 0, 0, 0)]
    [InlineData(double.NaN, 20, 0, 0)]
    public void S662_whole_rows_count_only_rows_that_fit_whole(double height, double row, double gap, int expected)
        => Assert.Equal(expected, Disclosure.WholeRows(height, row, gap));

    [Fact]
    public void S662_a_list_takes_whole_rows_from_what_the_ladder_left()
    {
        var room = new Disclosure(300, 200, gap: 8);
        room.Reserve(40);

        // 200 - 40 - 8 = 152 left: three 40px rows with 4px between (128), not a fourth (172).
        Assert.Equal(3, room.TakeRows(40, wanted: 10, rowGap: 4));
        Assert.Equal(24, room.Remaining, 3);
    }

    [Fact]
    public void S662_a_list_never_takes_more_rows_than_it_has()
    {
        var room = new Disclosure(300, 1000);

        Assert.Equal(2, room.TakeRows(40, wanted: 2));
        Assert.Equal(920, room.Remaining);
    }

    [Fact]
    public void S662_the_empty_state_line_budget_counts_rows_the_same_way()
    {
        Assert.Equal(Disclosure.WholeRows(99.6, 20), EmptyStateFit.LineBudget(99.6, 20));
        Assert.Equal(1, EmptyStateFit.LineBudget(5, 20));
    }

    // The chart's window now comes from the span setting: see MeterFrameworkTests (S-1121).

    [Fact]
    public void S665_a_larger_clock_adds_the_week_and_the_zone()
    {
        var config = new ClockConfig { TimeZoneId = "Etc/UTC" };
        var details = ClockFormatter.Details(new DateTimeOffset(2026, 10, 4, 13, 52, 0, TimeSpan.Zero), config, Locale.En);

        Assert.Equal("Sun, Oct 4", details.ShortDate);
        Assert.Equal("Week 40 · Day 277", details.Week);
        Assert.StartsWith("UTC", details.Zone, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 0, "UTC")]
    [InlineData(2, 0, "UTC+02:00")]
    [InlineData(-3, -30, "UTC-03:30")]
    [InlineData(5, 45, "UTC+05:45")]
    public void S665_the_offset_is_written_as_windows_writes_it(int hours, int minutes, string expected)
        => Assert.Equal(expected, ClockFormatter.Offset(new TimeSpan(hours, minutes, 0)));

    [Fact]
    public void S665_the_week_is_the_iso_week_across_a_year_boundary()
    {
        var config = new ClockConfig { TimeZoneId = "Etc/UTC" };

        // 1 January 2027 is a Friday, so it still belongs to ISO week 53 of 2026.
        var details = ClockFormatter.Details(new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero), config, Locale.En);

        Assert.Equal("Week 53 · Day 1", details.Week);
    }
}
