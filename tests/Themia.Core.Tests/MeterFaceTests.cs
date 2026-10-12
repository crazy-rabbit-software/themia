using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1762] The system meters' face: the reading and its history, the history filling the tile.
/// The areas are the seven size tiers less the header (the content box a meter is given).
/// </summary>
public sealed class MeterFaceTests
{
    private const double Inset = HeroLayout.SideInset;
    private const double Top = HeroLayout.TopInset;
    private const double Gap = 8;
    private const double Floor = 24;

    private static MeterFacePlan Plan(double width, double height, bool graph = true, TypeStep? fixedStep = null, bool icon = false)
        => MeterFace.Plan(new WidgetArea(width, height), graph, 4, icon, Gap, Floor, fixedStep);

    [Fact]
    public void S1762_a_tiny_tile_is_the_reading_alone_filling_it()
    {
        var plan = Plan(120, 76);

        Assert.Equal(MeterFaceArrangement.ReadingOnly, plan.Arrangement);
        Assert.False(plan.Graph);
        Assert.Equal(0, plan.GraphHeight);
        Assert.Equal(MeterFace.Reading(120 - (2 * Inset), 76, 4, false, FluentType.Title), plan.Reading);
    }

    [Theory]
    [InlineData(200, 106)]   // S
    [InlineData(320, 196)]   // M
    [InlineData(200, 356)]   // Tall
    [InlineData(560, 376)]   // L
    [InlineData(800, 516)]   // XL
    public void S1762_the_history_fills_everything_the_reading_leaves(double width, double height)
    {
        var plan = Plan(width, height);

        Assert.Equal(MeterFaceArrangement.Stacked, plan.Arrangement);

        // No empty space: the reading's line, the gap and the history, which reaches the card's
        // bottom edge ([S-1824]), are the whole tile.
        Assert.Equal(height, Top + plan.Reading.LineHeight + Gap + plan.GraphHeight, 6);

        // The history is never a sliver: at least the floor, and half the tile once there is room.
        Assert.True(plan.GraphHeight >= Floor);
        Assert.True(plan.GraphHeight >= Math.Min(height * MeterFace.GraphShare, height - Top - FluentType.Subtitle.LineHeight - Gap) - 1);

        // [S-3860] The reading never grows past the tier's step beside a history, nor below Subtitle.
        Assert.InRange(plan.Reading.LineHeight, FluentType.Subtitle.LineHeight, HeroLayout.TierStep(new WidgetArea(width, height)).LineHeight + 1e-6);
    }

    [Fact]
    public void S1762_a_larger_tile_gives_its_extra_room_to_the_history()
    {
        var m = Plan(320, 196);
        var xl = Plan(800, 516);

        Assert.True(xl.GraphHeight > m.GraphHeight);
        Assert.True(xl.GraphHeight > xl.Reading.LineHeight * 3);
    }

    [Fact]
    public void S1762_a_wide_strip_puts_the_history_beside_the_reading_at_full_height()
    {
        var plan = Plan(480, 116);

        Assert.Equal(MeterFaceArrangement.Side, plan.Arrangement);
        Assert.Equal(116, plan.GraphHeight);
        Assert.InRange(plan.ReadingColumn, 480 * MeterFace.SideColumnShare, 480 * MeterFace.SideColumnCap);
        Assert.True(plan.Reading.LineHeight <= 116);
    }

    [Theory]
    [InlineData(120, 76)]
    [InlineData(320, 196)]
    [InlineData(480, 116)]
    [InlineData(800, 516)]
    public void S1762_without_the_history_the_reading_fills_the_tile(double width, double height)
    {
        var plan = Plan(width, height, graph: false);
        var fitted = MeterFace.Reading(width - (2 * Inset), height, 4, false, FluentType.Display);

        Assert.Equal(MeterFaceArrangement.ReadingOnly, plan.Arrangement);
        Assert.Equal(HeroLayout.Grow(fitted, width - (2 * Inset), height, 4, 0), plan.Reading);
        Assert.True(plan.Reading.FontSize >= HeroLayout.TierStep(new WidgetArea(width, height)).FontSize || plan.Reading == fitted);
    }

    [Fact]
    public void S1762_a_fixed_step_keeps_the_reading_and_gives_the_rest_to_the_history()
    {
        var plan = Plan(380, 250, fixedStep: FluentType.Subtitle);

        Assert.Equal(FluentType.Subtitle, plan.Reading);
        Assert.Equal(250 - Top - FluentType.Subtitle.LineHeight - Gap, plan.GraphHeight, 6);
    }

    [Fact]
    public void S1762_a_box_too_short_for_the_floor_shows_no_history()
    {
        // Regular wide, but only room for the reading itself.
        var plan = Plan(300, Top + FluentType.Subtitle.LineHeight + Gap + Floor - 1);

        Assert.Equal(MeterFaceArrangement.ReadingOnly, plan.Arrangement);
    }

    [Fact]
    public void S1824_the_history_is_the_cards_bottom_band_with_no_inset_under_it()
    {
        // Room for the reading, the gap and the least history is enough: no bottom inset is spent.
        var fits = Plan(300, Top + FluentType.Subtitle.LineHeight + Gap + Floor);

        Assert.Equal(MeterFaceArrangement.Stacked, fits.Arrangement);
        Assert.Equal(Floor, fits.GraphHeight, 6);
    }

    [Fact]
    public void S1762_an_unmeasured_box_stacks_without_a_size()
    {
        var plan = MeterFace.Plan(default, true, 4, false, Gap, Floor);

        Assert.Equal(MeterFaceArrangement.Stacked, plan.Arrangement);
        Assert.Equal(FluentType.Subtitle, plan.Reading);
    }

    [Theory]
    [InlineData(120, 76, 28)]   // XS: the reading alone, at the tier's step
    [InlineData(200, 106, 28)]  // S
    [InlineData(320, 196, 28)]  // M
    [InlineData(480, 116, 28)]  // Wide
    [InlineData(200, 356, 28)]  // Tall
    [InlineData(560, 376, 40)]  // L
    [InlineData(800, 516, 68)]  // XL
    public void S3860_the_reading_is_the_tier_ladders_step_as_a_stock_price_beside_its_chart(double width, double height, double size)
    {
        var plan = Plan(width, height);

        Assert.Equal(size, plan.Reading.FontSize);
        Assert.Equal(HeroLayout.TierStep(new WidgetArea(width, height)), plan.Reading);
    }

    [Fact]
    public void S3860_a_battery_glyph_costs_the_reading_width_never_height()
    {
        // Narrow enough that the glyph's width matters: the reading steps down rather than overflowing.
        var bare = Plan(160, 300, icon: false);
        var badged = Plan(160, 300, icon: true);

        Assert.True(badged.Reading.FontSize <= bare.Reading.FontSize);
        Assert.True(HeroLayout.IconSize(badged.Reading) + HeroLayout.IconGap + FluentType.EstimateWidth(badged.Reading, 4) <= 160 - (2 * Inset) + 1e-6
            || badged.Reading == FluentType.Subtitle);
    }
}
