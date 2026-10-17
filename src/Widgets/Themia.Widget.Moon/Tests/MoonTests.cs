using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Moon.Tests;

/// <summary>
/// [S-3105] [S-6051] The Moon widget: its ladder and the month of phases. Moved with the widget
/// from Core's SkyWidgetsTests and TimeStylesTests, unchanged in what they assert.
/// </summary>
public sealed class MoonTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static MoonPlan Plan(double width, double height)
    {
        var area = new WidgetArea(width, height);
        var compact = area.WidthClass == SizeClass.Compact || area.HeightClass <= SizeClass.Regular;
        return MoonLayout.Plan(area, compact ? 32 : 40, 15);
    }

    [Fact]
    public void S3105_the_disc_is_never_dropped_and_stays_inside_its_floor_and_ceiling()
    {
        foreach (var tier in WidgetTiers.All)
        {
            var plan = Plan(tier.Width, tier.Height - 40);
            Assert.InRange(plan.Disc, MoonLayout.DiscFloor, MoonLayout.DiscCeiling);
            Assert.Equal(0, plan.Disc % 2);
        }

        Assert.Equal(MoonLayout.DiscFloor, Plan(10, 10).Disc);
    }

    [Fact]
    public void S3105_a_tiny_tile_is_the_disc_and_a_tall_one_has_every_row()
    {
        var tiny = Plan(120, 60);
        Assert.Equal(0, tiny.Rows);
        Assert.False(tiny.Side);

        var tall = Plan(200, 360);
        Assert.True(tall.Name && tall.Percent);
        Assert.Equal(MoonLayout.Ladder.Count, tall.Rows);
    }

    [Fact]
    public void S3105_the_name_comes_before_the_percentage_and_both_before_the_rows()
    {
        // Just room for the disc and one line: the name, not the percentage (a Compact column, so
        // the short tile is not read as a strip).
        var plan = Plan(170, MoonLayout.DiscFloor + MoonLayout.Gap * 2 + MoonLayout.NameStep.LineHeight);

        Assert.True(plan.Name);
        Assert.False(plan.Percent);
        Assert.Equal(0, plan.Rows);
    }

    [Fact]
    public void S3105_growing_the_tile_only_ever_adds_parts()
    {
        // Within one arrangement: a column from the height a 320 px tile stops being a strip.
        var before = Plan(320, 161);

        for (var height = 162.0; height <= 600; height += 1)
        {
            var plan = Plan(320, height);
            Assert.False(plan.Side);
            Assert.True(plan.Rows >= before.Rows || plan.Rows == MoonLayout.Ladder.Count, $"rows at {height}");
            Assert.True(!before.Name || plan.Name, $"name at {height}");
            Assert.True(!before.Percent || plan.Percent, $"percent at {height}");
            Assert.True(plan.Disc >= MoonLayout.DiscFloor);
            before = plan;
        }
    }

    [Fact]
    public void S3105_a_wide_strip_puts_the_text_beside_the_disc()
    {
        var plan = Plan(480, 120);

        Assert.True(plan.Side);
        Assert.True(plan.Name);
        Assert.True(plan.Disc <= 120);
    }

    [Fact]
    public void S3105_the_rows_run_rise_set_then_the_next_full_or_new_moon()
        => Assert.Equal([MoonRow.Rise, MoonRow.Set, MoonRow.NextMajor], MoonLayout.Ladder);

    [Fact]
    public void S6051_the_phase_month_is_every_day_of_this_month_today_marked_once()
    {
        var now = new DateTimeOffset(2026, 2, 10, 20, 0, 0, TimeSpan.FromHours(1));
        var days = MoonMonth.Days(now, Zurich);

        Assert.Equal(28, days.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), days[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 10), days.Single(d => d.Today).Date);

        // A day on, the moon is a 29.53rd of its cycle further, wrapping at the new moon.
        for (var i = 1; i < days.Count; i++)
        {
            var step = (days[i].Phase - days[i - 1].Phase + 1) % 1;
            Assert.Equal(1 / 29.53058867, step, 3);
        }

        Assert.Equal(MoonPhase.At(new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.FromHours(1))).Phase, days[9].Phase, 9);
    }
}
