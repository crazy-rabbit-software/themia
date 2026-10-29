using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Moon.Tests;

/// <summary>
/// [S-3105] [S-6051] The Moon widget: its ladder on the family's blocks and the month of phases.
/// </summary>
public sealed class MoonTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    /// <summary>The view's area at a tier: the card less the header.</summary>
    private static MoonPlan Plan(double width, double height) => MoonLayout.Plan(new WidgetArea(width, height - FamilySpace.ContentTop));

    [Fact]
    public void S3105_the_lit_share_is_a_readout_on_the_areas_step_and_never_a_bigger_moon()
    {
        Assert.Equal(28, Plan(200, 150).Face.Hero.FontSize);
        Assert.Equal(40, Plan(320, 240).Face.Hero.FontSize);
        Assert.Equal(68, Plan(800, 560).Face.Hero.FontSize);

        // The moon leads the readout at its step: never larger than the family's 68.
        foreach (var tier in WidgetTiers.All)
        {
            Assert.InRange(Plan(tier.Width, tier.Height).Face.Hero.FontSize, 28, 68);
        }
    }

    [Fact]
    public void S3105_the_moon_gives_way_before_the_figure_where_the_readout_is_too_narrow_for_both()
    {
        // 120 wide: "100%" at 28 and the moon beside it want 97, the content has 88.
        var tiny = Plan(120, 120);
        Assert.False(tiny.Disc);
        Assert.Equal(28, tiny.Face.Hero.FontSize);
        Assert.Equal(0, tiny.Face.ListRows);

        Assert.True(Plan(200, 150).Disc);
        Assert.True(Plan(320, 240).Disc);
    }

    [Fact]
    public void S3105_rows_follow_the_readout_in_whole_rows_of_the_tiers_lines()
    {
        var medium = Plan(320, 240);
        Assert.False(medium.Face.Side);
        Assert.Equal(2, medium.Face.ListLines);
        Assert.Equal(2, medium.Face.ListRows);

        var tall = Plan(200, 400);
        Assert.Equal(MoonLayout.Ladder.Count, tall.Face.ListRows);

        // A strip: compact height, one line a row.
        Assert.Equal(1, Plan(480, 160).Face.ListLines);
    }

    [Fact]
    public void S3105_an_expanded_width_puts_the_rows_beside_the_readout()
    {
        var wide = Plan(480, 160);
        Assert.True(wide.Face.Side);
        Assert.Equal(Math.Floor(480 * DetailFace.HeroShare), wide.Face.HeroColumn);
        Assert.Equal(0, wide.Face.RowsTop);
        Assert.Equal(MoonLayout.Ladder.Count, wide.Face.ListRows);
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
