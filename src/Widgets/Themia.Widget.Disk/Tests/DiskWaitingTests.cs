using Themia.App.Widgets.Meters;
using Xunit;

namespace Themia.Widget.Disk.Tests;

/// <summary>[S-15164] The disk's bars before the counters' first rate.</summary>
public sealed class DiskWaitingTests
{
    /// <summary>
    /// [S-15164] The bars style drew its drive glyph and "--" alone, no rows, until the counters' second collection gave a
    /// rate (a render under load caught it so). Before a rate its rows stand as they will, each "--" without a bar (WR-10).
    /// </summary>
    [Fact]
    public void S15164_before_a_rate_the_bars_are_read_and_write_each_without_a_reading()
    {
        var waiting = new DiskMeter(null!).WaitingItems;

        Assert.Equal(2, waiting.Count);
        Assert.All(waiting, item =>
        {
            Assert.Null(item.Fraction);
            Assert.Equal("--", item.Value);
            Assert.False(string.IsNullOrWhiteSpace(item.Label));
        });
        Assert.NotEqual(waiting[0].Label, waiting[1].Label);
    }
}
