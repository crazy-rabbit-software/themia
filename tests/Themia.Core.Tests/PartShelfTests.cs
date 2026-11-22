using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-15537] The parts' shelf: the parts offered laid on the desktop's grid cells.</summary>
public class PartShelfTests
{
    [Fact]
    public void S15537_parts_stand_in_reading_order_a_cell_apart_their_names_under_them()
    {
        var shelf = PartShelf.Lay([(8, 4), (4, 4), (10, 2), (20, 3)], 24);

        // 8 and 4 fit the first line with a cell round and between them; 10 would end at cell 25 of 24, so it wraps.
        Assert.Equal([new ShelfBox(1, 1, 8, 4), new ShelfBox(10, 1, 4, 4), new ShelfBox(1, 8, 10, 2), new ShelfBox(1, 13, 20, 3)], shelf.Boxes);
        Assert.Equal((24, 19), (shelf.Width, shelf.Height));
    }

    [Fact]
    public void S15537_no_part_overlaps_another_or_its_name_and_none_leaves_the_shelf()
    {
        (int W, int H)[] sizes = [(9, 4), (8, 8), (24, 4), (7, 4), (20, 3), (20, 1), (7, 4), (4, 4), (10, 4), (20, 3), (20, 3)];
        foreach (var width in new[] { 10, 24, 30, 48, 90 })
        {
            var shelf = PartShelf.Lay(sizes, width);
            Assert.Equal(sizes.Length, shelf.Boxes.Count);
            for (var i = 0; i < shelf.Boxes.Count; i++)
            {
                var a = shelf.Boxes[i];
                Assert.Equal(sizes[i], (a.W, a.H));
                Assert.True(a.X >= 1 && a.Y >= 1 && a.X + a.W <= shelf.Width - 1 && a.Y + a.H + PartShelf.Caption <= shelf.Height - 1, $"{a} in {shelf.Width} x {shelf.Height}");
                for (var j = i + 1; j < shelf.Boxes.Count; j++)
                {
                    var b = shelf.Boxes[j];
                    var apart = a.X + a.W + 1 <= b.X || b.X + b.W + 1 <= a.X || a.Y + a.H + PartShelf.Caption + 1 <= b.Y || b.Y + b.H + PartShelf.Caption + 1 <= a.Y;
                    Assert.True(apart, $"{a} and {b} at width {width}");
                }
            }
        }
    }

    [Fact]
    public void S15537_it_is_never_narrower_than_its_head_or_its_widest_part()
    {
        Assert.Equal(PartShelf.LeastWidth, PartShelf.Lay([(4, 4)], 3).Width);
        Assert.Equal(42, PartShelf.Lay([(40, 2)], 10).Width);
        Assert.Equal((PartShelf.LeastWidth, 2), (PartShelf.Lay([], 0).Width, PartShelf.Lay([], 0).Height));
    }

    [Fact]
    public void S15537_a_short_room_gets_a_wider_shelf_of_fewer_lines()
    {
        (int W, int H)[] sizes = [.. Enumerable.Repeat((8, 4), 12)];

        var tall = PartShelf.Fit(sizes, most: 200, widest: 100);
        var low = PartShelf.Fit(sizes, most: 20, widest: 100);

        Assert.Equal(PartShelf.LeastWidth, tall.Width);
        Assert.True(low.Width > tall.Width && low.Height <= 20, $"{low.Width} x {low.Height}");

        // No width fits: the widest it may be.
        Assert.Equal(30, PartShelf.Fit(sizes, most: 3, widest: 30).Width);
    }
}
