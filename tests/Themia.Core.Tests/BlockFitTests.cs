using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12808] A one-readout block keeps its parts together (owner, 2026-11-19: "Keep them together") and scales up as
/// one until the room's height or width stops it.
/// </summary>
public class BlockFitTests
{
    [Fact]
    public void S12808_at_its_natural_room_nothing_scales_or_moves()
    {
        Assert.Equal(new BlockLay(1, 0), BlockFit.Lay(156, 52, 156, 52));
    }

    [Fact]
    public void S12808_a_taller_and_wider_room_scales_it_to_the_height()
    {
        // Twice as tall, three times as wide: the height stops it at 2, its top on the room's.
        Assert.Equal(new BlockLay(2, 0), BlockFit.Lay(468, 104, 156, 52));
    }

    [Fact]
    public void S12808_where_the_width_stops_it_it_is_centred_in_the_height_left()
    {
        // As wide as twice the natural, four times as tall: scale 2, the 104 tall block centred in 208.
        Assert.Equal(new BlockLay(2, 52), BlockFit.Lay(312, 208, 156, 52));
    }

    [Theory]
    [InlineData(156, 400)]   // taller only: as wide as natural, so no larger, centred
    [InlineData(900, 52)]    // wider only: as tall as natural
    [InlineData(100, 40)]    // smaller than natural: never shrinks, from the top
    public void S12808_it_never_shrinks_and_never_grows_past_either_side(double width, double height)
    {
        var lay = BlockFit.Lay(width, height, 156, 52);
        Assert.Equal(1, lay.Scale);
        Assert.Equal(Math.Max(0, (height - 52) / 2), lay.Top);
    }

    [Fact]
    public void S12808_a_scaled_block_never_passes_the_room()
    {
        foreach (var (w, h) in new[] { (300.0, 400.0), (520, 600), (900, 300), (3840, 120) })
        {
            var lay = BlockFit.Lay(w, h, 156, 52);
            Assert.True(156 * lay.Scale <= w + 1e-9 && 52 * lay.Scale <= h + 1e-9, $"{w}x{h}: {lay}");
            Assert.True(lay.Top >= 0 && lay.Top + (52 * lay.Scale) <= h + 1e-9);
        }
    }

    [Fact]
    public void S12808_bad_input_is_harmless()
    {
        Assert.Equal(new BlockLay(1, 0), BlockFit.Lay(double.NaN, 100, 156, 52));
        Assert.Equal(new BlockLay(1, 0), BlockFit.Lay(300, 400, 0, 52));
    }

    [Fact]
    public void S12808_where_the_height_stops_it_its_first_ink_stands_on_the_top_and_its_last_on_the_foot()
    {
        // A reading and its caption planned in 176 x 52, 6 of leading over the reading's capitals and 6.7 under the
        // caption's baseline: in a wide room the ink fills the height, the leading outside it.
        var ink = new LineInk(6, 6.7);
        var lay = BlockFit.Lay(2000, 276, 176, 52, ink);

        Assert.Equal(276 / (52 - 12.7), lay.Scale, 6);
        Assert.Equal(0, lay.Top + (6 * lay.Scale), 6);
        Assert.Equal(276, lay.Top + ((52 - 6.7) * lay.Scale), 6);
    }

    [Fact]
    public void S12808_at_its_natural_room_its_ink_stays_where_the_plan_put_it()
    {
        // Width-bound at 1: the ink centred in the natural room, as the plan centred it.
        var lay = BlockFit.Lay(176, 52, 176, 52, new LineInk(6, 6));
        Assert.Equal(new BlockLay(1, 0), lay);
    }

    [Fact]
    public void S12814_a_bar_never_holds_the_block_small_its_text_and_bar_reach_the_top_and_the_foot()
    {
        // The owner's drive card, 330x130 (306 x 106 inside the insets): an icon and "47%" with its line under (60 wide,
        // 52 tall, the 28 readout's leading 9.2 over its figures) over a bar 4 tall, 8 under it. The bar spans any width,
        // so only the text's 60 limits the width: the height does, at 106 / (64 - 9.2).
        var ink = new LineInk(9.2, 0);
        var fill = BlockFit.Fill(306, 106, 60, 64, ink);

        Assert.Equal(106 / (64 - 9.2), fill.Scale, 6);
        Assert.Equal(-9.2 * fill.Scale, fill.Top, 6);

        // Laid in the whole height: its first ink on the top, its last (the bar's foot) on the foot.
        Assert.Equal(0, fill.Top + (ink.Over * fill.Scale), 6);
        Assert.Equal(106, fill.Top + (fill.Height * fill.Scale), 6);
    }

    [Theory]
    [InlineData(330, 130)]
    [InlineData(350, 70)]
    [InlineData(350, 80)]
    [InlineData(330, 140)]
    [InlineData(300, 164)]
    [InlineData(600, 200)]
    [InlineData(520, 600)]
    [InlineData(900, 300)]
    public void S12814_at_every_desktop_size_the_ink_spans_the_room_top_to_foot(double width, double height)
    {
        // A reading and its caption (156 wide, 52 tall) with a control row (28): whatever limits the scale, the room
        // left goes between the parts, never over the first ink or under the last.
        var ink = new LineInk(9.2, 0);
        var fill = BlockFit.Fill(width, height, 156, 80, ink);

        Assert.True(fill.Scale >= 1);
        Assert.True(156 * fill.Scale <= width + 1e-6 || fill.Scale == 1);
        Assert.Equal(0, fill.Top + (ink.Over * fill.Scale), 6);
        Assert.Equal(Math.Max(height, (80 - 9.2) * fill.Scale), fill.Top + ((fill.Height - ink.Under) * fill.Scale), 6);
    }

    [Fact]
    public void S12813_a_placeholder_keeps_its_readings_size_and_stands_its_dashes_on_the_top()
    {
        // The drive card without its drive: "--" and "%" on the 28 readout (36 line), its reason under it, its bar.
        // The dashes ink lower than figures would; sized by the figures, placed by the dashes.
        var step = new TypeStep(28, 36, true);
        var held = LineInk.Of(step);
        var drawn = LineInk.Placeholder(step, 0);
        Assert.True(drawn.Over > held.Over);

        var reading = BlockFit.Fill(306, 106, 60, 64, held);
        var fill = BlockFit.Fill(306, 106, 60, 64, held, drawn);

        Assert.Equal(reading.Scale, fill.Scale, 6);
        Assert.Equal(0, fill.Top + (drawn.Over * fill.Scale), 6);
        Assert.Equal(106, fill.Top + ((fill.Height - drawn.Under) * fill.Scale), 6);
    }

    [Fact]
    public void S12814_a_width_held_block_keeps_a_pixel_over_its_text_so_rounding_never_trims_it()
    {
        // The weather's "--" over "Can't reach the service" (150 wide) in a tall card: the width holds the scale.
        var fill = BlockFit.Fill(306, 400, 150, 52, new LineInk(9.2, 4));

        Assert.Equal(306 / (150 + BlockFit.WidthSlack), fill.Scale, 6);
        Assert.True((306 / fill.Scale) - 150 >= BlockFit.WidthSlack - 1e-9);
    }

    [Theory]
    [InlineData("--", true)]
    [InlineData("–", true)]
    [InlineData("- -", true)]
    [InlineData("47", false)]
    [InlineData("-4", false)]
    [InlineData("", false)]
    public void S12813_only_dashes_dots_and_spaces_are_a_placeholder(string text, bool capless)
    {
        Assert.Equal(capless, LineInk.Capless(text));
    }

    [Fact]
    public void S12813_a_placeholders_unit_capitals_stand_higher_than_its_dashes()
    {
        var step = new TypeStep(28, 36, true);
        var bare = LineInk.Placeholder(step, 0);
        var unit = LineInk.Placeholder(step, 20);

        Assert.Equal((36 * LineInk.FaceBaseline) - (28 * LineInk.FaceDash), bare.Over, 6);
        Assert.Equal((36 * LineInk.FaceBaseline) - (20 * LineInk.FaceCap), unit.Over, 6);
        Assert.Equal(LineInk.Of(step).Under, unit.Under, 6);
    }

    [Fact]
    public void S12814_a_room_shorter_than_the_parts_keeps_them_packed_from_the_top()
    {
        var fill = BlockFit.Fill(300, 40, 100, 80, new LineInk(6, 4));
        Assert.Equal(new BlockFill(1, 80, -6), fill);
    }

    [Fact]
    public void S12814_justified_parts_take_the_room_between_them_and_one_part_takes_it_all()
    {
        var two = ItemStretch.Reach(100, [StretchItem.Fixed(36), StretchItem.Fixed(28)], justify: true);
        Assert.Equal(new ItemSlot(0, 36), two[0]);
        Assert.Equal(new ItemSlot(72, 28), two[1]);

        var one = ItemStretch.Reach(100, [StretchItem.Fixed(36)], justify: true);
        Assert.Equal(new ItemSlot(0, 100), one[0]);
    }
}
