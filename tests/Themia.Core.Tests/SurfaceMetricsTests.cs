using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-854] Nothing writes geometry by a surface's provisional metrics: the settled signal and the gate.</summary>
public sealed class SurfaceMetricsTests
{
    private static readonly ScreenMetrics Provisional = new(3840, 2160);
    private static readonly ScreenMetrics Real = new(2560, 1440);

    [Fact]
    public void S854_the_scale_is_settled_only_once_the_root_has_loaded_with_a_real_scale()
    {
        Assert.Null(SurfaceMetrics.SettledScale(loaded: false, 1.5));
        Assert.Null(SurfaceMetrics.SettledScale(loaded: true, null));
        Assert.Null(SurfaceMetrics.SettledScale(loaded: true, 0));
        Assert.Null(SurfaceMetrics.SettledScale(loaded: true, double.NaN));
        Assert.Equal(1.5, SurfaceMetrics.SettledScale(loaded: true, 1.5));
    }

    [Fact]
    public void S854_the_desktop_settles_only_when_every_surface_has()
    {
        var home = new SurfaceMetrics("home", Real, 1, Settled: true);
        var system = new SurfaceMetrics("system", Provisional, 1, Settled: false);

        Assert.False(SurfaceMetrics.AllSettled([]));
        Assert.False(SurfaceMetrics.AllSettled([home, system]));
        Assert.True(SurfaceMetrics.AllSettled([home, system with { Screen = Real, Settled = true }]));
        Assert.False((home with { Screen = default }).Writable);
    }
}
