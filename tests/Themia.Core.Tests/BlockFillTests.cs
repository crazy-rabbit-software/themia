using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12808] A one-readout block keeps its parts together (owner, 2026-11-19: "Keep them together") and scales up as
/// one until the room's height or width stops it.
/// </summary>
public class BlockFillTests
{
    /// <summary>A block as the one-part panel lays it (the clock's face): its scale and its box's top.</summary>
    private static (double Scale, double Top) Lay(double width, double height, double naturalWidth, double naturalHeight, LineInk ink = default, double least = 1)
    {
        var fill = BlockLayout.Fill(width, height, naturalWidth, naturalHeight, ink, least);
        return (fill.Scale, fill.Top + 0.0);
    }

    [Fact]
    public void S12808_at_its_natural_room_nothing_scales_or_moves()
    {
        Assert.Equal((1, 0), Lay(156, 52, 156, 52));
    }

    [Fact]
    public void S12808_a_taller_and_wider_room_scales_it_to_the_height()
    {
        // Twice as tall, three times as wide: the height stops it at 2, its top on the room's.
        Assert.Equal((2, 0), Lay(468, 104, 156, 52));
    }

    [Fact]
    public void S12808_where_the_width_stops_it_it_is_centred_in_the_height_left()
    {
        // As wide as twice the natural, four times as tall: scale 2, the 104 tall block centred in 208.
        Assert.Equal((2, 52), Lay(312, 208, 156, 52));
    }

    [Theory]
    [InlineData(156, 400)]   // taller only: as wide as natural, so no larger, centred
    [InlineData(900, 52)]    // wider only: as tall as natural
    [InlineData(100, 40)]    // smaller than natural: never shrinks, from the top
    public void S12808_it_never_shrinks_and_never_grows_past_either_side(double width, double height)
    {
        var lay = Lay(width, height, 156, 52);
        Assert.Equal(1, lay.Scale);
        Assert.Equal(Math.Max(0, (height - 52) / 2), lay.Top);
    }

    [Fact]
    public void S12808_a_scaled_block_never_passes_the_room()
    {
        foreach (var (w, h) in new[] { (300.0, 400.0), (520, 600), (900, 300), (3840, 120) })
        {
            var lay = Lay(w, h, 156, 52);
            Assert.True(156 * lay.Scale <= w + 1e-9 && 52 * lay.Scale <= h + 1e-9, $"{w}x{h}: {lay}");
            Assert.True(lay.Top >= 0 && lay.Top + (52 * lay.Scale) <= h + 1e-9);
        }
    }

    [Fact]
    public void S12808_bad_input_is_harmless()
    {
        Assert.Equal((1, 0), Lay(double.NaN, 100, 156, 52));
        Assert.Equal((1, 0), Lay(300, 400, 156, double.NaN));
    }

    [Fact]
    public void S12808_where_the_height_stops_it_its_first_ink_stands_on_the_top_and_its_last_on_the_foot()
    {
        // A reading and its caption planned in 176 x 52, 6 of leading over the reading's capitals and 6.7 under the
        // caption's baseline: in a wide room the ink fills the height, the leading outside it.
        var ink = new LineInk(6, 6.7);
        var lay = Lay(2000, 276, 176, 52, ink);

        Assert.Equal(276 / (52 - 12.7), lay.Scale, 6);
        Assert.Equal(0, lay.Top + (6 * lay.Scale), 6);
        Assert.Equal(276, lay.Top + ((52 - 6.7) * lay.Scale), 6);
    }

    [Fact]
    public void S12808_at_its_natural_room_its_ink_stays_where_the_plan_put_it()
    {
        // Width-bound at 1: the ink centred in the natural room, as the plan centred it.
        var lay = Lay(176, 52, 176, 52, new LineInk(6, 6));
        Assert.Equal((1, 0), lay);
    }

    [Fact]
    public void S12814_a_bar_never_holds_the_block_small_its_text_and_bar_reach_the_top_and_the_foot()
    {
        // The owner's drive card, 330x130 (306 x 106 inside the insets): an icon and "47%" with its line under (60 wide,
        // 52 tall, the 28 readout's leading 9.2 over its figures) over a bar 4 tall, 8 under it. The bar spans any width,
        // so only the text's 60 limits the width: the height does, at 106 / (64 - 9.2).
        var ink = new LineInk(9.2, 0);
        var fill = BlockLayout.Fill(306, 106, 60, 64, ink);

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
    public void S14280_at_every_desktop_size_the_parts_stay_together_and_the_block_fills_one_axis(double width, double height)
    {
        // A reading and its caption (156 wide, 52 tall) with a control row (28), WR-7 (owner, 2026-11-19: "Keep them
        // together"; "All together, scaled"): the block is laid in its parts' own height, never in the room's, so nothing
        // goes between them; it scales until its ink fills the height or its parts fill the width.
        var ink = new LineInk(9.2, 0);
        var fill = BlockLayout.Fill(width, height, 156, 80, ink);
        var first = fill.Top + (ink.Over * fill.Scale);
        var last = fill.Top + ((fill.Height - ink.Under) * fill.Scale);

        Assert.Equal(80, fill.Height, 6);
        Assert.True(fill.Scale >= 1);
        Assert.True(first >= -1e-6 && last <= Math.Max(height, (80 - 9.2) * fill.Scale) + 1e-6, $"{width}x{height}: {first}..{last}");
        var fillsHeight = Math.Abs(first) < 1e-6 && Math.Abs(last - height) < 1e-6;
        var fillsWidth = Math.Abs((156 * fill.Scale) - width) < 1e-6;
        Assert.True(fillsHeight || fillsWidth || fill.Scale == 1, $"{width}x{height}: scale {fill.Scale}, ink {first}..{last}");
    }

    [Fact]
    public void S14280_where_the_width_stops_it_the_block_keeps_its_parts_together_centred_on_its_ink()
    {
        // The exchange rate's 600x200 card (576 x 188 in its insets): "1.0739 EUR +0.13%" over "per CHF" (180 wide,
        // 52 tall, 9.2 of leading over the figures, 4 under the caption's baseline). The width stops the scale; the
        // parts stay packed and the height left stands alike over the first ink and under the last ([S-12808]), never
        // between the reading and its caption (WR-7).
        var ink = new LineInk(9.2, 4);
        var fill = BlockLayout.Fill(576, 188, 180, 52, ink);

        Assert.Equal(576.0 / 180, fill.Scale, 6);
        Assert.Equal(52, fill.Height, 6);
        var over = fill.Top + (ink.Over * fill.Scale);
        var under = 188 - (fill.Top + ((fill.Height - ink.Under) * fill.Scale));
        Assert.True(over > 1, $"{over}");
        Assert.Equal(over, under, 6);
    }

    [Fact]
    public void S14280_a_regression_the_old_fill_laid_a_width_held_block_in_the_whole_height()
    {
        // Before: a block the width held was laid in the room's height divided by the scale, its caption pushed to the
        // foot ("1.0739 EUR" on the top edge, "per CHF" on the foot, a band between them).
        var fill = BlockLayout.Fill(576, 188, 180, 52, new LineInk(9.2, 4));
        Assert.True(fill.Height * fill.Scale < 188 - 1, $"laid {fill.Height} at {fill.Scale}");
    }

    [Fact]
    public void S12813_a_placeholder_is_sized_and_placed_by_the_ink_it_draws()
    {
        // The drive card without its drive (330x140: 306 x 128 inside the insets): "--" and "%" on the 28 readout (36
        // line), its reason under it, its bar. The dashes ink lower than figures would; the block is sized and placed by
        // the ink it draws, so its dashes stand on the top and its bar's foot on the foot (WR-1, WR-4).
        var step = new TypeStep(28, 36, true);
        var drawn = LineInk.Placeholder(step, 0);
        Assert.True(drawn.Over > LineInk.Of(step).Over);

        var fill = BlockLayout.Fill(306, 128, 60, 64, drawn);

        Assert.Equal(128 / (64 - drawn.Over - drawn.Under), fill.Scale, 6);
        Assert.Equal(0, fill.Top + (drawn.Over * fill.Scale), 6);
        Assert.Equal(128, fill.Top + ((fill.Height - drawn.Under) * fill.Scale), 6);
    }

    [Fact]
    public void S12813_a_regression_sized_by_the_reading_it_stood_for_the_placeholder_left_a_band_at_the_foot()
    {
        // Before: the fit sized the block by the capitals of the reading the dashes stood for and placed it by the dashes,
        // so the difference stood under its last ink: 20 DIP at the owner's 330x140 drive card ("Not connected": 13.3 over
        // it, 32.7 under it). Sized by the ink it draws, the foot band is gone.
        var step = new TypeStep(28, 36, true);
        var reading = LineInk.Of(step);
        var drawn = LineInk.Placeholder(step, 0);

        var old = BlockLayout.Fill(306, 128, 60, 64, reading);
        var oldTop = old.Top + ((reading.Over - drawn.Over) * old.Scale);
        var oldFoot = 128 - (oldTop + ((old.Height - drawn.Under) * old.Scale));
        Assert.True(oldFoot > 10, $"{oldFoot}");

        var fill = BlockLayout.Fill(306, 128, 60, 64, drawn);
        Assert.Equal(0, 128 - (fill.Top + ((fill.Height - drawn.Under) * fill.Scale)), 6);
    }

    [Fact]
    public void S14301_a_regression_the_slack_in_the_scale_stood_a_width_held_block_short_of_the_end()
    {
        // Before: the scale held a pixel over the parts' width, so a block the width held stood that pixel, scaled, short
        // of the end's inset: 4.5 DIP at the exchange rate's 900x300 card (876 wide inside its insets, "1.0739 EUR +0.13%"
        // 180 wide), its right band 16.7 against the 12 the left one had.
        var fill = BlockLayout.Fill(876, 288, 180, 52, new LineInk(9.2, 4));
        Assert.Equal(876, 180 * fill.Scale, 6);
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
    public void S12833_a_smaller_room_scales_the_block_down_to_its_floor_and_no_further()
    {
        // A reading of 28 over a caption of 12 (156 x 52): its least scale is the caption's at 9 DIP.
        var least = ScaleFloor.Least(12);
        Assert.Equal(0.75, least, 6);

        var half = BlockLayout.Fill(78, 26, 156, 52, new LineInk(9.2, 4), least: least);
        Assert.Equal(least, half.Scale, 6);

        var most = BlockLayout.Fill(140, 52, 156, 52, new LineInk(9.2, 4), least: least);
        Assert.InRange(most.Scale, least, 1);
        Assert.Equal(140.0 / 156, most.Scale, 6);
    }

    [Fact]
    public void S12833_the_scale_never_falls_as_the_room_grows()
    {
        var least = ScaleFloor.Least(12);
        var last = 0.0;
        foreach (var room in new[] { 20.0, 40, 60, 80, 120, 200, 400, 800 })
        {
            var fill = BlockLayout.Fill(room * 3, room, 156, 52, new LineInk(9.2, 4), least: least);
            Assert.True(fill.Scale >= last - 1e-9, $"room {room}: {fill.Scale} under {last}");
            last = fill.Scale;
        }
    }

    [Theory]
    [InlineData(12, 0, 0.75)]
    [InlineData(9, 0, 1)]
    [InlineData(6, 0, 1)]
    [InlineData(28, 16, 0.75)]
    [InlineData(40, 0, 0.225)]
    [InlineData(0, 0, 1)]
    public void S12833_the_floor_is_the_smallest_texts_at_9_or_glyphs_at_12(double text, double glyph, double least)
        => Assert.Equal(least, ScaleFloor.Least(text, glyph), 6);

    [Fact]
    public void S12833_a_face_that_scales_down_is_sized_down_to_its_floor_never_under_the_least_widget()
    {
        var sizing = WidgetSizing.Range(new(200, 80), new(200, 80), new(400, 80)).ScalesDown(0.5);
        Assert.Equal(new SizeDip(100, 40), sizing.Min);
        Assert.Equal(new SizeDip(200, 80), sizing.Natural);

        var tiny = WidgetSizing.Range(new(40, 40), new(40, 40), new(80, 40)).ScalesDown(0.25);
        Assert.Equal(new SizeDip(WidgetSizing.LeastScaled, WidgetSizing.LeastScaled), tiny.Min);
    }

    [Fact]
    public void S12814_a_room_shorter_than_the_parts_keeps_them_packed_from_the_top()
    {
        var fill = BlockLayout.Fill(300, 40, 100, 80, new LineInk(6, 4));
        Assert.Equal(new BlockFill(1, 80, -6), fill);
    }

    [Fact]
    public void S12814_justified_parts_take_the_room_between_them_and_one_part_takes_it_all()
    {
        var two = ItemSize.Lay(100, [StackItem.Fixed(36), StackItem.Fixed(28)], ItemGrid.Default(), justify: true);
        Assert.Equal(new ItemSlot(0, 36), two[0]);
        Assert.Equal(new ItemSlot(72, 28), two[1]);

        var one = ItemSize.Lay(100, [StackItem.Fixed(36)], ItemGrid.Default(), justify: true);
        Assert.Equal(new ItemSlot(0, 100), one[0]);
    }

    /// <summary>
    /// [S-14791] A block holding a line it cuts never grows past where that line stands whole (owner, 2026-11-20: "the next
    /// meeting clips the time into the title"): the meeting's title alone in a 370 x 80 card, 280 wide whole, asked for its
    /// least 84 and filled the 56 tall room at 2.8 times, drawn 56 DIP and cut to "Design re…". Held by its whole width it
    /// stands whole at 1.24; a room too narrow for it at the floor cuts it there, never under the floor.
    /// </summary>
    [Fact]
    public void S14791_a_cut_line_never_lets_the_block_grow_past_where_it_stands_whole()
    {
        var ink = LineInk.Of(FamilyType.Title);
        var least = ScaleFloor.Least(20);

        // The regression: asked for its least alone, the title filled the height.
        var cut = BlockLayout.Fill(346, 56, 84, 28, ink, least);
        Assert.True(cut.Scale > 2.5, $"{cut.Scale}");
        Assert.True(280 * cut.Scale > 346);

        // Held by its whole width: it stands whole, on both side insets.
        var whole = BlockLayout.Fill(346, 56, 84, 28, ink, least, BlockLayout.Uncut(346, 280));
        Assert.Equal(346 / 280.0, whole.Scale, 6);
        Assert.True(280 * whole.Scale <= 346 + 1e-9);

        // Where the height stops it first, the height's scale.
        Assert.Equal(cut.Scale, BlockLayout.Fill(346, 56, 84, 28, ink, least, BlockLayout.Uncut(346, 50)).Scale, 6);

        // Too narrow even at the floor: the floor, and the line is cut there.
        Assert.Equal(least, BlockLayout.Fill(100, 56, 84, 28, ink, least, BlockLayout.Uncut(100, 280)).Scale, 6);

        // Nothing held: no most.
        Assert.Equal(double.PositiveInfinity, BlockLayout.Uncut(346, 0));
        Assert.Equal(double.PositiveInfinity, BlockLayout.Uncut(double.NaN, 280));
    }
}
