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
}
