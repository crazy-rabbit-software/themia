using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3732] Where a line dragged up or down a menu's list lands.</summary>
public sealed class ListDropTests
{
    /// <summary>Three 32-DIP lines from the top.</summary>
    private static readonly double[] Middles = [16, 48, 80];

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(10, 0)]
    [InlineData(20, 1)]
    [InlineData(47, 1)]
    [InlineData(60, 2)]
    [InlineData(81, 3)]
    [InlineData(400, 3)]
    public void S3732_the_gap_is_before_the_first_line_whose_middle_is_below_the_pointer(double y, int gap)
        => Assert.Equal(gap, ListDrop.Gap(Middles, y));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 2, 1)]
    [InlineData(0, 3, 2)]
    [InlineData(2, 0, 0)]
    [InlineData(2, 2, 2)]
    [InlineData(2, 3, 2)]
    [InlineData(1, 0, 0)]
    public void S3732_the_gaps_either_side_of_the_dragged_line_leave_it_where_it_is(int from, int gap, int target)
        => Assert.Equal(target, ListDrop.Target(from, gap));

    [Fact]
    public void S3732_an_empty_list_has_one_gap()
        => Assert.Equal(0, ListDrop.Gap([], 100));
}
