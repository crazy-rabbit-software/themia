using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3802] Putting a settings list in order: one place by the keyboard, onto another item by a drag.</summary>
public sealed class DeskListOrderTests
{
    [Fact]
    public void S3802_a_drop_puts_the_dragged_item_in_the_targets_place_both_ways()
    {
        var items = new List<string> { "a", "b", "c", "d" };

        Assert.True(DeskListOrder.MoveTo(items, 0, 2));
        Assert.Equal(["b", "c", "a", "d"], items);

        Assert.True(DeskListOrder.MoveTo(items, 3, 0));
        Assert.Equal(["d", "b", "c", "a"], items);
    }

    [Fact]
    public void S3802_a_drop_on_itself_or_out_of_range_changes_nothing()
    {
        var items = new List<string> { "a", "b", "c" };

        Assert.False(DeskListOrder.MoveTo(items, 1, 1));
        Assert.False(DeskListOrder.MoveTo(items, -1, 0));
        Assert.False(DeskListOrder.MoveTo(items, 0, 3));
        Assert.False(DeskListOrder.MoveTo<string>(null, 0, 1));
        Assert.Equal(["a", "b", "c"], items);
    }

    [Fact]
    public void S3802_one_place_stops_at_the_ends()
    {
        var items = new List<string> { "a", "b", "c" };

        Assert.True(DeskListOrder.Move(items, 1, -1));
        Assert.Equal(["b", "a", "c"], items);
        Assert.False(DeskListOrder.Move(items, 0, -1));
        Assert.False(DeskListOrder.Move(items, 2, +1));
        Assert.False(DeskListOrder.Move(items, 1, 0));
        Assert.Equal(["b", "a", "c"], items);
    }
}
