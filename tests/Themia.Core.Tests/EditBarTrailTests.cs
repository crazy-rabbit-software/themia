using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3507] The edit bar's trail: one width always, the crumbs shrinking alike on a narrow display.</summary>
public sealed class EditBarTrailTests
{
    [Fact]
    public void S3507_the_trail_reserves_every_crumb_at_its_widest()
    {
        Assert.Equal(170 + 160 + 160 + (2 * 20), EditBarTrail.Reserve);
        Assert.Equal([170.0, 160, 160], EditBarTrail.Widths(EditBarTrail.Reserve));
        Assert.Equal([170.0, 160, 160], EditBarTrail.Widths(2000));
    }

    [Fact]
    public void S3507_short_of_room_every_crumb_gives_up_the_same()
    {
        var widths = EditBarTrail.Widths(EditBarTrail.Reserve - 60);

        Assert.Equal([150.0, 140, 140], widths.Select(w => Math.Round(w, 6)));
    }

    [Fact]
    public void S3507_a_crumb_at_its_least_stays_there_and_the_others_give_up_the_rest()
    {
        // 160 → 96 is 64 each (192 in all); the next 5 come off the display's alone.
        var widths = EditBarTrail.Widths(EditBarTrail.Reserve - 192 - 5);

        Assert.Equal([170 - 64 - 5, 96.0, 96], widths.Select(w => Math.Round(w, 6)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(double.NaN)]
    public void S3507_never_under_the_least(double room)
    {
        Assert.All(EditBarTrail.Widths(room), w => Assert.Equal(EditBarTrail.Min, w, 6));
        Assert.Equal((3 * 96) + 40, EditBarTrail.Least);
    }
}
