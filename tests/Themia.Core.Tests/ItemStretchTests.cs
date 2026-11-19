using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12400] [S-12410] [S-12807] A widget's items fill its content (owner, 2026-11-19: "the items have no spacing
/// between them and they try to take up the whole space from top to bottom and left to right"): rows grow until they
/// touch and fill the room; texts keep their size.
/// </summary>
public class ItemStretchTests
{
    [Theory]
    [InlineData(112, 4, 28)]     // four natural rows: natural
    [InlineData(160, 4, 40)]     // more room: each row a quarter of it, touching
    [InlineData(1000, 4, 250)]   // no cap: the rows take it all
    [InlineData(90, 4, 28)]      // short of room: natural rows from the top (a scroll)
    public void S12807_rows_grow_until_they_touch_and_fill_the_room(double available, int count, double pitch)
    {
        var reach = ItemStretch.Reach(available, count, 28);

        Assert.Equal(pitch, reach.Pitch);
        Assert.True(reach.Length(count) <= Math.Max(available, count * 28) + 1e-9);
    }

    [Fact]
    public void S12807_a_rows_height_is_whole_device_pixels_the_rest_at_the_end()
    {
        var reach = ItemStretch.Reach(181, 6, 28, scale: 1.25);

        Assert.Equal(Math.Round(reach.Pitch * 1.25), reach.Pitch * 1.25, 6);
        Assert.InRange(181 - reach.Length(6), 0, 6 / 1.25);
    }

    [Fact]
    public void S12807_a_views_rows_run_from_its_top_to_the_foots_inset()
    {
        // [S-12815] A view of four rows and the foot's 12 under the card's top inset: the rows run from the card's top
        // edge to its foot edge, 136, touching.
        Assert.Equal(ItemStretch.Reach(136, 4, 28), ItemStretch.Rows(124, 4, 1));
        Assert.Equal(new RowReach(46), ItemStretch.Rows(172, 4, 1));

        // Under a tool line, a search box or column titles: from the view's top.
        Assert.Equal(new RowReach(40), ItemStretch.Rows(172, 4, 1, reserved: 12, edge: false));
    }

    [Fact]
    public void S12807_rows_over_an_add_row_end_where_it_starts()
    {
        // Four rows, the add row of 28 on the card's foot edge, the rows from its top edge.
        const double View = (4 * 40) + 28 + 12;
        Assert.Equal(new RowReach(Math.Floor((View + 12 - 28) / 4)), ItemStretch.RowsAbove(View, 4, 1));
    }

    [Fact]
    public void S12807_unlike_items_share_the_room_the_growing_ones_take_it()
    {
        // A row (28), a fixed readout of 36, a two-line row (40) in 140: 36 spare, 18 to each row; they touch.
        var slots = ItemStretch.Reach(140, [StretchItem.Row(1), StretchItem.Fixed(36), StretchItem.Row(2)]);

        Assert.Equal(new ItemSlot(0, 46), slots[0]);
        Assert.Equal(new ItemSlot(46, 36), slots[1]);
        Assert.Equal(new ItemSlot(82, 58), slots[2]);
    }

    [Fact]
    public void S12807_with_nothing_to_grow_the_items_stand_together_centred()
    {
        var slots = ItemStretch.Reach(100, [StretchItem.Fixed(36), StretchItem.Fixed(16)], gap: 8);

        Assert.Equal(new ItemSlot(20, 36), slots[0]);
        Assert.Equal(new ItemSlot(64, 16), slots[1]);
    }

    [Fact]
    public void S12807_items_never_overlap_nor_shrink()
    {
        foreach (var room in new[] { 0.0, 37, 60, 113, 200, 999 })
        {
            var items = new[] { StretchItem.Row(1), StretchItem.Row(3), StretchItem.Fixed(24), StretchItem.Row(2) };
            var slots = ItemStretch.Reach(room, items, gap: 4);
            for (var i = 0; i < items.Length; i++)
            {
                Assert.True(slots[i].Length >= items[i].Natural - 1e-9);
            }

            for (var i = 1; i < slots.Length; i++)
            {
                Assert.True(slots[i].Start >= slots[i - 1].End + 4 - 1e-9, $"room {room}: item {i} overlaps");
            }
        }
    }

    [Fact]
    public void S12807_nothing_to_lay_and_bad_input_are_harmless()
    {
        Assert.Empty(ItemStretch.Reach(100, []));
        Assert.Equal(new RowReach(28), ItemStretch.Reach(double.NaN, 3, 28));
        Assert.Equal(new RowReach(28), ItemStretch.Reach(100, 0, 28));
        var slots = ItemStretch.Reach(double.NaN, [new StretchItem(double.NaN, true), StretchItem.Fixed(-5)], gap: double.NaN, scale: 0);
        Assert.All(slots, s => Assert.Equal(0, s.Length));
    }

    [Fact]
    public void S12807_a_line_inks_from_its_capitals_to_its_baseline()
    {
        // The family's face on a line of the family's: a Body line (14/20) inks its capitals 6.2 under its top and
        // stands its baseline 4 over its foot; a 28 readout (28/36) 9.2 and 7.2. What a one-readout block and a clock
        // fill their box with ([S-12808]).
        Assert.Equal(6.2, LineInk.Of(FamilyType.Body).Over, 6);
        Assert.Equal(4, LineInk.Of(FamilyType.Body).Under, 6);
        Assert.Equal(9.2, LineInk.Of(FamilyType.Big28).Over, 6);
        Assert.Equal(7.2, LineInk.Of(FamilyType.Big28).Under, 6);
        Assert.Equal(new LineInk(2, 2), LineInk.Row(1, FamilyShape.Icon));
        Assert.Equal(LineInk.None, LineInk.Of(new TypeStep(double.NaN, 20, false)));
    }
}
