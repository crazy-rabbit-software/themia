using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12400] [S-12410] A widget's items fill its content: the spare room first stretches them toward their
/// most, evenly, and what they cannot take is spacing as the rest says; never an item past its most.
/// </summary>
public class ItemStretchTests
{
    private static StretchItem[] Rows(int n, double natural = 28) => [.. Enumerable.Repeat(StretchItem.Capped(natural, ItemStretch.RowCap), n)];

    private static StretchItem[] Fixed(params double[] lengths) => [.. lengths.Select(StretchItem.Fixed)];

    private static StretchItem[] Free(params double[] lengths) => [.. lengths.Select(StretchItem.Free)];

    [Fact]
    public void S12400_free_items_share_the_spare_room_evenly_and_reach_the_end()
    {
        var slots = ItemStretch.Lay(140, Free(28, 28, 28, 28));

        Assert.Equal([0, 35, 70, 105], slots.Select(s => s.Start));
        Assert.All(slots, s => Assert.Equal(35, s.Length));
        Assert.Equal(140, slots[^1].End);
    }

    [Fact]
    public void S12410_rows_stretch_to_half_again_and_no_further()
    {
        // 4 rows of 28 in 168: 14 each would do, 168 / 4 = 42 = 28 x 1.5 exactly.
        Assert.All(ItemStretch.Lay(168, Rows(4)), s => Assert.Equal(42, s.Length));

        // Twice the room: each stops at 42, the rest is spacing round them.
        var slots = ItemStretch.Lay(224, Rows(4));
        Assert.All(slots, s => Assert.Equal(42, s.Length));
        Assert.Equal([7, 63, 119, 175], slots.Select(s => s.Start));
    }

    [Fact]
    public void S12410_an_item_never_passes_its_most_whatever_the_room()
    {
        foreach (var room in new[] { 0.0, 50, 113, 200, 999, 5000 })
        {
            var items = new[] { StretchItem.Capped(28, 1.5), StretchItem.Fixed(40), StretchItem.Capped(56, 1.5), StretchItem.Free(20) };
            var slots = ItemStretch.Lay(room, items, gap: 8);
            for (var i = 0; i < items.Length; i++)
            {
                Assert.InRange(slots[i].Length, items[i].Natural, items[i].Max);
            }

            // In order, never overlapping, the gap kept.
            for (var i = 1; i < slots.Length; i++)
            {
                Assert.True(slots[i].Start >= slots[i - 1].End + 8 - 1e-9, $"room {room}: item {i} overlaps");
            }
        }
    }

    [Fact]
    public void S12410_the_room_a_capped_item_cannot_take_goes_to_the_others()
    {
        // 60 spare: the fixed one takes none, the free one all of it.
        var slots = ItemStretch.Lay(120, [StretchItem.Fixed(30), StretchItem.Free(30)]);

        Assert.Equal(30, slots[0].Length);
        Assert.Equal(90, slots[1].Length);
        Assert.Equal(120, slots[1].End);
    }

    [Theory]
    [InlineData(StretchRest.Start, new double[] { 0, 20 })]
    [InlineData(StretchRest.End, new double[] { 60, 80 })]
    [InlineData(StretchRest.Centre, new double[] { 30, 50 })]
    [InlineData(StretchRest.Between, new double[] { 0, 80 })]
    [InlineData(StretchRest.Around, new double[] { 15, 65 })]
    public void S12410_the_rest_goes_where_the_kind_says(StretchRest rest, double[] starts)
        => Assert.Equal(starts, ItemStretch.Lay(100, Fixed(20, 20), rest: rest).Select(s => s.Start));

    [Fact]
    public void S12410_one_item_between_is_centred()
        => Assert.Equal(40, ItemStretch.Lay(100, Fixed(20), rest: StretchRest.Between)[0].Start);

    [Theory]
    [InlineData(112)]
    [InlineData(111.5)]
    [InlineData(100)]
    public void S12400_no_spare_or_too_little_room_keeps_the_items_natural_lengths_from_the_start(double available)
    {
        var slots = ItemStretch.Lay(available, Rows(4));

        Assert.Equal([0, 28, 56, 84], slots.Select(s => s.Start));
        Assert.All(slots, s => Assert.Equal(28, s.Length));
    }

    [Fact]
    public void S12400_gaps_stay_and_the_stretch_adds_to_them()
    {
        var slots = ItemStretch.Lay(100, Free(20, 20, 20), gap: 8);

        Assert.All(slots, s => Assert.Equal(28, s.Length, 6));
        Assert.Equal([0, 36, 72], slots.Select(s => s.Start));
    }

    [Fact]
    public void S12400_unequal_items_each_grow_by_the_same_share_whole_pixels_first_ones_first()
    {
        var slots = ItemStretch.Lay(100, Free(10, 30, 20));

        // 40 spare pixels among three: 14, 13, 13.
        Assert.Equal([24, 43, 33], slots.Select(s => s.Length));
    }

    [Fact]
    public void S12400_lengths_and_starts_are_whole_device_pixels_at_150_percent()
    {
        // 10 DIPs of spare at 1.5 is 15 pixels among four rows (each may take 21): 4, 4, 4, 3.
        var slots = ItemStretch.Lay(122, Rows(4), scale: 1.5);

        Assert.All(slots, s => Assert.Equal(Math.Round(s.Length * 1.5), s.Length * 1.5, 6));
        Assert.All(slots, s => Assert.Equal(Math.Round(s.Start * 1.5), s.Start * 1.5, 6));
        Assert.Equal(122, slots[^1].End, 6);
    }

    [Fact]
    public void S12400_nothing_to_lay_and_bad_input_are_harmless()
    {
        Assert.Empty(ItemStretch.Lay(100, []));
        var slots = ItemStretch.Lay(double.NaN, [new StretchItem(double.NaN, double.NaN), StretchItem.Fixed(-5)], gap: double.NaN, scale: 0);
        Assert.All(slots, s => Assert.Equal(0, s.Length));
    }

    [Theory]
    [InlineData(180, 6, 28, 30)]   // the foot's 12 shared by six rows
    [InlineData(400, 6, 28, 42)]   // capped at half again
    [InlineData(100, 6, 28, 28)]   // short of room: the natural
    [InlineData(52, 1, 40, 52)]
    public void S12410_a_lists_pitch_fills_its_room_up_to_the_cap(double room, int rows, double natural, double pitch)
        => Assert.Equal(pitch, ItemStretch.Pitch(room, rows, natural));

    [Fact]
    public void S12410_a_pitch_is_whole_device_pixels()
    {
        var pitch = ItemStretch.Pitch(181, 6, 28, scale: 1.25);
        Assert.Equal(Math.Round(pitch * 1.25), pitch * 1.25, 6);
        Assert.True(pitch * 6 <= 181);
    }
}
