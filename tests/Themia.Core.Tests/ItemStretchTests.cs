using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12400] [S-12410] [S-12807] A list's rows fill its content: their first and last ink reach its ends, the
/// room between stretches their bands toward their most (never past the inset), the rest is spacing.
/// </summary>
public class ItemStretchTests
{
    [Theory]
    [InlineData(104, 28, 4, 0)]       // four natural rows less their ends' bands: natural, the bands over the ends
    [InlineData(112, 30, 5, 0.667)]   // eight more: the room between the texts grows, half to each band (whole pixels)
    [InlineData(160, 42, 11, 4.667)]  // capped at half again (a band of 11): the rest is spacing between them
    [InlineData(90, 22, 1, 1.333)]    // less room than their bands: the bands narrow, the texts still reach both ends
    public void S12807_rows_reach_both_ends_with_their_text(double available, double pitch, double over, double gap)
    {
        var reach = ItemStretch.Reach(available, 4, 28, 4);

        Assert.Equal((pitch, over, gap), (reach.Pitch, reach.Over, Math.Round(reach.Gap, 3)));

        // The first text stands on the start and the last text's end on the end.
        Assert.Equal(available, reach.Length(4) - (2 * reach.Over), 6);
    }

    [Fact]
    public void S12807_one_row_is_centred_in_its_room()
    {
        // 20 of text in 40: 10 over and under it, the row stretched round it (a band of 10, under the cap of 11).
        Assert.Equal(new RowReach(40, 0, 0, 0), ItemStretch.Reach(40, 1, 28, 4));

        // Less than its own bands: its bands narrow round its text, still centred.
        Assert.Equal(new RowReach(24, 0, 0, 0), ItemStretch.Reach(24, 1, 28, 4));

        // Less than its text: natural, from the top.
        Assert.Equal(new RowReach(28, 4, 0, 8), ItemStretch.Reach(16, 1, 28, 4));
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
    public void S12807_a_rows_height_is_whole_device_pixels_and_its_rows_reach_the_end()
    {
        var reach = ItemStretch.Reach(181, 6, 28, 4, scale: 1.25);

        foreach (var length in new[] { reach.Pitch, reach.Over })
        {
            Assert.Equal(Math.Round(length * 1.25), length * 1.25, 6);
        }

        Assert.Equal(181, reach.Length(6) - reach.Over - reach.Under, 6);
    }

    [Fact]
    public void S12807_unlike_items_reach_the_ends_each_round_its_own_content()
    {
        // A one-line row (20 of text, a band of 4) and a two-line one (36, a band of 2) in 100: 44 between them.
        var slots = ItemStretch.Reach(100, [new StretchItem(28, 42, 4), new StretchItem(40, 60, 2)]);

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
                Assert.InRange(slots[i].Length, items[i].Natural - (2 * items[i].Band), Math.Min(items[i].Max, items[i].Natural - (2 * items[i].Band) + 24));
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
        // A view of four rows and the foot's 12: their ink spans 112.
        Assert.Equal(ItemStretch.Reach(112, 4, 28, 4, ink: LineInk.Row(1)), ItemStretch.Rows(124, 4, 1));
    }

    [Fact]
    public void S12807_rows_over_an_add_row_end_where_it_starts_its_text_on_the_foots_inset()
    {
        // Four rows, the add row of 28 and the foot's 12; the add row's ink (its + glyph, 2 over its line's end) on
        // the foot's inset, so its box stands 6 over the view's foot.
        const double View = (4 * 28) + 28 + 12;
        var reach = ItemStretch.RowsAbove(View, 4, 1);
        var lastEnd = reach.Length(4) - reach.Over;
        var addTop = View - (12 - 4 - 2) - 28;
        Assert.Equal(22, ItemStretch.FootRow());

        Assert.True(lastEnd <= addTop + 1e-9, $"{lastEnd} past the add row at {addTop}");
        Assert.True(addTop - lastEnd <= 1e-6, "no band left between the rows and the add row");
    }

    [Fact]
    public void S12807_nothing_to_lay_and_bad_input_are_harmless()
    {
        Assert.Empty(ItemStretch.Reach(100, []));
        Assert.Equal(new RowReach(28, 4, 0, 4), ItemStretch.Reach(double.NaN, 3, 28, 4));
        Assert.Equal(new RowReach(28, 4, 0, 4), ItemStretch.Reach(100, 0, 28, 4));
        var slots = ItemStretch.Reach(double.NaN, [new StretchItem(double.NaN, double.NaN, double.NaN), StretchItem.Fixed(-5)], gap: double.NaN, scale: 0);
        Assert.All(slots, s => Assert.Equal(0, s.Length));
    }

    [Fact]
    public void S12807_a_line_inks_from_its_capitals_to_its_baseline()
    {
        // The family's face on a line of the family's: a Body line (14/20) inks its capitals 6.2 under its top and
        // stands its baseline 4 over its foot; a 28 readout (28/36) 9.2 and 7.2.
        Assert.Equal(6.2, LineInk.Of(FamilyType.Body).Over, 6);
        Assert.Equal(4, LineInk.Of(FamilyType.Body).Under, 6);
        Assert.Equal(9.2, LineInk.Of(FamilyType.Big28).Over, 6);
        Assert.Equal(7.2, LineInk.Of(FamilyType.Big28).Under, 6);

        // A row led by a 16 glyph inks from the glyph, 2 into its line; a two-line row's foot is its Caption's baseline.
        Assert.Equal(new LineInk(2, 2), LineInk.Row(1, FamilyShape.Icon));
        Assert.Equal(3.2, LineInk.Row(2).Under, 6);
        Assert.Equal(LineInk.None, LineInk.Of(new TypeStep(double.NaN, 20, false)));
    }

    [Theory]
    [InlineData(152, 5, 0)]     // the forecast's natural box: five rows led by a glyph
    [InlineData(152, 5, 16)]
    [InlineData(376, 5, 16)]    // a tall box: the rows at their cap, the rest spacing
    [InlineData(124, 4, 0)]
    [InlineData(301, 7, 16)]    // odd room, a pixel left over
    public void S12807_the_first_and_last_ink_stand_on_the_insets(double height, int count, double lead)
    {
        // Owner, 2026-11-19: "the items still dont reach to the top and bottom. they still have a little / small gap".
        foreach (var scale in new[] { 1.0, 1.5 })
        {
            var reach = ItemStretch.Rows(height, count, 1, scale: scale, lead: lead);
            var ink = LineInk.Row(1, lead);
            var halo = (reach.Pitch - 20) / 2;

            // The first row's box starts Over before the view's top: its band, then its leading, then its ink at 0.
            Assert.Equal(0, -reach.Over + halo + ink.Over, 6);

            // The last row's ink ends on the foot's inset line, within the sub-pixel rest.
            var lastInk = -reach.Over + reach.Length(count) - halo - ink.Under;
            Assert.InRange(height - 12 - lastInk, -1e-9, 1 / scale);
            Assert.Equal(reach.Length(count) - reach.Over - reach.Under, height - 12, 0);

            // No box reaches past the card's edge (the bleed is the inset).
            Assert.True(reach.Over <= 12 + 1e-9 && reach.Under <= 12 + 1e-9, $"{reach} past the edge");
        }
    }

    [Fact]
    public void S12807_unlike_items_stand_their_ends_ink_on_the_ends()
    {
        // A glyph-led row over a 28 readout: the row's glyph on the start, the readout's baseline on the end.
        var items = new[] { StretchItem.Row(1, FamilyShape.Icon), StretchItem.Fixed(36, LineInk.Of(FamilyType.Big28)) };
        var slots = ItemStretch.Reach(100, items);

        Assert.Equal(0, slots[0].Start + ((slots[0].Length - 20) / 2) + 2, 6);
        Assert.InRange(slots[1].End - 7.2, 99, 100 + 1e-9);   // whole pixels: the sub-pixel rest at the end
    }

    [Theory]
    [InlineData(71)]     // just room for them: no gap
    [InlineData(200)]
    [InlineData(377)]    // odd room
    public void S12808_a_readouts_parts_reach_the_insets_and_the_room_goes_between_them(double room)
    {
        // Owner, 2026-11-19 ("Yes, those too"): a 28 reading, its Caption line under it and a 28 control line.
        var items = new[]
        {
            StretchItem.Fixed(36, LineInk.Of(FamilyType.Big28)),
            StretchItem.Fixed(16, LineInk.Of(FamilyType.Caption)),
            StretchItem.Fixed(28),
        };
        var slots = ItemStretch.Reach(room, items, scale: 1.5);

        // The parts never grow; the reading's capitals on the top, the control's box on the end.
        Assert.Equal(new[] { 36d, 16, 28 }, slots.Select(s => s.Length));
        Assert.Equal(0, slots[0].Start + 9.2, 6);
        Assert.InRange(room - slots[2].End, 0, 1);

        // The room between them is shared alike (whole pixels: within one).
        var first = slots[1].Start - slots[0].End;
        var second = slots[2].Start - slots[1].End;
        Assert.InRange(Math.Abs(first - second), 0, 1);
    }

    [Fact]
    public void S12808_one_line_alone_is_centred_by_its_ink()
    {
        // A 28 reading alone in 100: its ink (19.6) has 40.2 over and under it.
        var slot = ItemStretch.Reach(100, [StretchItem.Fixed(36, LineInk.Of(FamilyType.Big28))])[0];
        var top = slot.Start + 9.2;
        var foot = 100 - (slot.End - 7.2);
        Assert.InRange(foot - top, 0, 1);
    }
}
