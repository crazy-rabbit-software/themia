using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The styles of the network, disk, battery, ping and drive widgets ([S-6261]..[S-6274]): which
/// styles each kind has, what each draws, and the geometry of the battery, the signal bars, the
/// numbers.
/// </summary>
public sealed class IoStylesTests
{
    private const double Mb = 1024 * 1024;

    // ---- [S-6263] signal bars ------------------------------------------------------------------

    [Theory]
    [InlineData(null, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(-1.0, 0)]
    [InlineData(0.0, 4)]
    [InlineData(50.0, 4)]
    [InlineData(50.5, 3)]
    [InlineData(100.0, 3)]
    [InlineData(150.0, 2)]
    [InlineData(200.0, 2)]
    [InlineData(201.0, 1)]
    [InlineData(5000.0, 1)]
    public void S6263_the_bars_a_probe_earns_follow_the_latency(double? ms, int bars)
        => Assert.Equal(bars, PingSignal.Level(ms));

    // ---- [S-6262] the battery shape ------------------------------------------------------------

    // ---- [S-6261] a picture and its figure ----------------------------------------------------

    // ---- [S-6264] the network's numbers -------------------------------------------------------

    // ---- [S-6271] the disk's read and write bars ----------------------------------------------

    [Fact]
    public void S6271_the_bars_read_on_the_graphs_own_top()
    {
        // Idle: a background murmur against the 1 MB/s floor stays a sliver.
        var (read, write) = DiskRules.BarShares(new DiskRates(0.1 * Mb, 0.05 * Mb), []);
        Assert.Equal(0.1, read, 3);
        Assert.Equal(0.05, write, 3);

        // A copy at 40 MB/s: the top is the doubling past it (64 MB/s), the larger bar the larger share.
        (read, write) = DiskRules.BarShares(new DiskRates(32 * Mb, 8 * Mb), []);
        Assert.Equal(0.5, read, 3);
        Assert.Equal(0.125, write, 3);

        // The window's peak holds the top up after the copy ends, as the graph's does.
        (read, _) = DiskRules.BarShares(new DiskRates(2 * Mb, 0), [100 * Mb]);
        Assert.Equal(2.0 / 128, read, 3);

        // A bad rate is no bar.
        (read, _) = DiskRules.BarShares(new DiskRates(double.NaN, 0), []);
        Assert.Equal(0, read);
    }
}

file static class RectExtensions
{
    public static double Right(this Themia.Core.Geometry.PixelRect rect) => rect.X + rect.Width;
}
