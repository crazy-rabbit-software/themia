using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12400] [S-12410] [S-12807] A list's rows fill its content: their first and last text reach its ends, the
/// room between stretches their bands toward their most (never past the inset), the rest is spacing.
/// </summary>
public class ItemStretchTests
{
    [Theory]
    [InlineData(104, 28, 4, 0)]    // four natural rows less their ends' bands: natural, the bands over the ends
    [InlineData(112, 30, 5, 0)]    // eight more: the room between the texts grows, half to each band
    [InlineData(160, 42, 11, 4)]   // capped at half again (a band of 11): the rest is spacing between them
    [InlineData(90, 28, 4, 0)]     // short of room: natural rows from the top, the first text on it
    public void S12807_rows_reach_both_ends_with_their_text(double available, double pitch, double over, double gap)
    {
        var reach = ItemStretch.Reach(available, 4, 28, 4);

        Assert.Equal((pitch, over, gap), (reach.Pitch, reach.Over, reach.Gap));

        // The first text stands on the start; the last text's end never passes the end (the sub-pixel rest is left).
        var textEnd = reach.Length(4) - (2 * reach.Over);
        Assert.True(textEnd <= Math.Max(available, 104) + 1e-9, $"{textEnd} past {available}");
    }

    [Fact]
    public void S12807_one_row_is_centred_in_its_room()
    {
        // 20 of text in 40: 10 over and under it, the row stretched round it (a band of 10, under the cap of 11).
        Assert.Equal(new RowReach(40, 0, 0), ItemStretch.Reach(40, 1, 28, 4));

        // Less than its own bands: natural, from the top.
        Assert.Equal(new RowReach(28, 4, 0), ItemStretch.Reach(24, 1, 28, 4));
    }

    [Fact]
    public void S12807_a_band_never_passes_the_inset_so_a_fill_stays_inside_the_card()
    {
        // Three lines (56, a band of 2) may grow to 84, a band of 16: the inset holds it at 12.
        var reach = ItemStretch.Reach(1000, 3, 56, 2);

        Assert.Equal(12, reach.Over);
        Assert.Equal(52 + 24, reach.Pitch);
    }

    [Fact]
    public void S12807_reach_is_whole_device_pixels()
    {
        var reach = ItemStretch.Reach(181, 6, 28, 4, scale: 1.25);

        foreach (var length in new[] { reach.Pitch, reach.Over, reach.Gap })
        {
            Assert.Equal(Math.Round(length * 1.25), length * 1.25, 6);
        }
    }

    [Fact]
    public void S12807_unlike_items_reach_the_ends_each_round_its_own_content()
    {
        // A one-line row (20 of text, a band of 4) and a two-line one (36, a band of 2) in 100: 44 between them.
        var slots = ItemStretch.Reach(100, [StretchItem.Row(1), StretchItem.Row(2)]);

        Assert.Equal(new ItemSlot(-11, 42), slots[0]);   // its band grows to its cap, 11
        Assert.Equal(new ItemSlot(52, 60), slots[1]);    // its band to the inset, 12
        Assert.Equal(100, slots[1].End - 12);            // the last text ends on the end
        Assert.True(slots[0].End <= slots[1].Start);
    }

    [Fact]
    public void S12807_a_slot_never_passes_its_most_nor_the_bleed_and_never_overlaps()
    {
        foreach (var room in new[] { 0.0, 37, 60, 113, 200, 999 })
        {
            var items = new[] { StretchItem.Row(1), StretchItem.Row(3), StretchItem.Fixed(24), StretchItem.Row(2) };
            var slots = ItemStretch.Reach(room, items, gap: 4);
            for (var i = 0; i < items.Length; i++)
            {
                Assert.InRange(slots[i].Length, items[i].Natural, Math.Min(items[i].Max, items[i].Natural - (2 * items[i].Band) + 24));
            }

            for (var i = 1; i < slots.Length; i++)
            {
                Assert.True(slots[i].Start >= slots[i - 1].End + 4 - 1e-9, $"room {room}: item {i} overlaps");
            }
        }
    }

    [Fact]
    public void S12807_a_views_rows_run_from_its_top_to_the_foots_inset()
    {
        // A view of four rows and the foot's 12: the texts span 112.
        Assert.Equal(ItemStretch.Reach(112, 4, 28, 4), ItemStretch.Rows(124, 4, 1));
    }

    [Fact]
    public void S12807_rows_over_an_add_row_end_where_it_starts_its_text_on_the_foots_inset()
    {
        // Four rows, the add row of 28 and the foot's 12; the add row stands 8 over the view's foot.
        const double View = (4 * 28) + 28 + 12;
        var reach = ItemStretch.RowsAbove(View, 4, 1);
        var lastEnd = reach.Length(4) - reach.Over;
        var addTop = View - (12 - 4) - 28;

        Assert.True(lastEnd <= addTop + 1e-9, $"{lastEnd} past the add row at {addTop}");
        Assert.True(addTop - lastEnd <= 1, "no band left between the rows and the add row");
    }

    [Fact]
    public void S12807_nothing_to_lay_and_bad_input_are_harmless()
    {
        Assert.Empty(ItemStretch.Reach(100, []));
        Assert.Equal(new RowReach(28, 4, 0), ItemStretch.Reach(double.NaN, 3, 28, 4));
        Assert.Equal(new RowReach(28, 4, 0), ItemStretch.Reach(100, 0, 28, 4));
        var slots = ItemStretch.Reach(double.NaN, [new StretchItem(double.NaN, double.NaN, double.NaN), StretchItem.Fixed(-5)], gap: double.NaN, scale: 0);
        Assert.All(slots, s => Assert.Equal(0, s.Length));
    }
}
