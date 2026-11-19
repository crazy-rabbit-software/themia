using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12833] How small a face that scales goes (owner, 2026-11-19: "for the widgets that can scale there should basically be
/// no lower limit except for one that makes sense so you can still see the widget"; "the clock has a min height that is way
/// to big"): its main reading at the legibility floor, its smaller parts giving way on the way down.
/// </summary>
public sealed class FaceFloorTests
{
    /// <summary>A face of a 68 reading 175 wide (its ink 48), a 28 unit after it (55 wide) and a 14 line under it (16 more ink).</summary>
    private static readonly double[] Sizes = [68, 28, 14];

    private static FacePlan Plan(double floor)
        => new(175 + (floor <= 28 ? 55 : 0), 48 + (floor <= 14 ? 16 : 0));

    [Fact]
    public void S12833_the_main_reading_alone_sets_the_least_box_at_the_floor()
    {
        var least = FaceFloor.LeastBox(Sizes, Plan);

        // Not the line under's floor (9 / 14) nor the unit's (9 / 28): the reading's, 9 / 68.
        Assert.Equal(175 * 9 / 68.0, least.Width, 6);
        Assert.Equal(48 * 9 / 68.0, least.Height, 6);
    }

    [Fact]
    public void S12833_at_its_natural_room_every_part_is_drawn_and_nothing_caps_the_face()
    {
        var fit = FaceFloor.Fit(230, 64, Sizes, Plan);

        Assert.Equal(new FaceFit(14, 1, double.PositiveInfinity), fit);
        Assert.True(fit.Draws(14) && fit.Draws(28) && fit.Draws(68));
        Assert.Equal(FaceFit.All, FaceFloor.Fit(0, 64, Sizes, Plan));
        Assert.Equal(FaceFit.All, FaceFloor.Fit(double.NaN, 64, Sizes, Plan));
    }

    [Fact]
    public void S12833_the_smallest_part_gives_way_first_and_no_drawn_text_is_under_the_floor()
    {
        // Half the room: the line under would be 7, so it goes; the unit stays at 14.
        var half = FaceFloor.Fit(115, 32, Sizes, Plan);
        Assert.Equal(28, half.Floor);
        Assert.Equal(9 / 14.0, half.Most, 6);
        Assert.False(half.Draws(14));

        // A fifth: the unit would be under 9 too; the reading alone, at what the room gives it.
        var fifth = FaceFloor.Fit(46, 13, Sizes, Plan);
        Assert.Equal(68, fifth.Floor);
        Assert.InRange(fifth.Scale * 68, ScaleFloor.Text, 68);
        Assert.True(fifth.Scale <= fifth.Most);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.0, 1.0)]
    public void S12833_the_scale_and_the_parts_only_fall_as_the_room_shrinks(double byWidth, double byHeight)
    {
        // The room shrinks from the natural box towards nothing: proportionally, by its width alone or by its height
        // alone (the other axis kept). Dropping the unit makes the face narrower; it is never drawn larger for that.
        double lastScale = double.PositiveInfinity, lastFloor = 0;
        for (var t = 1.5; t >= 0.02; t -= 0.01)
        {
            var width = 230 * (byWidth > 0 ? t : 1.5);
            var height = 64 * (byHeight > 0 ? t : 1.5);
            var fit = FaceFloor.Fit(width, height, Sizes, Plan);

            Assert.True(fit.Scale <= lastScale + 1e-9, $"t {t:0.00}: {fit.Scale} over {lastScale}");
            Assert.True(fit.Floor >= lastFloor - 1e-9, $"t {t:0.00}: a part came back");
            foreach (var size in Sizes.Where(fit.Draws))
            {
                Assert.True(size * fit.Scale >= ScaleFloor.Text - 1e-6, $"t {t:0.00}: {size} drawn at {size * fit.Scale}");
            }

            lastScale = fit.Scale;
            lastFloor = fit.Floor;
        }

        // Down to the reading alone at the floor.
        Assert.Equal(68, lastFloor);
        Assert.Equal(ScaleFloor.Least(68), lastScale, 6);
    }

    [Fact]
    public void S12833_a_face_scales_down_to_its_least_box_never_under_the_least_widget_nor_over_its_stated_min()
    {
        var sizing = WidgetSizing.Range(new(250, 90), new(250, 90), new(500, 90)).ScalesDownTo(new(47.2, 30.3));
        Assert.Equal(new SizeDip(48, WidgetSizing.LeastScaled), sizing.Min);
        Assert.Equal(new SizeDip(250, 90), sizing.Natural);

        var stated = WidgetSizing.Range(new(40, 40), new(250, 90), new(500, 90)).ScalesDownTo(new(120, 60));
        Assert.Equal(new SizeDip(40, 40), stated.Min);

        var rows = WidgetSizing.Range(new(250, 90), new(250, 90), new(500, 90)).WithHeightSteps(new SizeSteps(20, 28, 1, 6, 2)).ScalesDownTo(new(60, 20));
        Assert.Equal(new SizeDip(60, rows.HeightSteps!.At(1)), rows.Min);
    }

    [Fact]
    public void S12833_a_block_is_never_drawn_over_its_most()
    {
        var free = BlockFit.Lay(468, 104, 156, 52, least: 0.1);
        Assert.Equal(2, free.Scale, 6);

        var capped = BlockFit.Lay(468, 104, 156, 52, least: 0.1, most: 0.5);
        Assert.Equal(0.5, capped.Scale, 6);
        Assert.Equal((104 - 26) / 2.0, capped.Top, 6);

        // Never under its least for all that.
        Assert.Equal(0.3, BlockFit.Lay(10, 10, 156, 52, least: 0.3, most: 0.5).Scale, 6);
    }
}
