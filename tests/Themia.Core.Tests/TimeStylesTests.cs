using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6061] The shared dot-grid rule (the moon's month; the sun's and the moon's own rules are their modules').
/// </summary>
public class TimeStylesTests
{
    // ---- the styles themselves ---------------------------------------------------------------

    // ---- the shared ring and dot grid ----------------------------------------------------------

    [Fact]
    public void S6061_the_dot_grid_reflows_to_the_box_and_every_dot_fits()
    {
        var wide = DotGrid.Fit(365, 448, 128);
        var tall = DotGrid.Fit(365, 168, 368);
        Assert.True(wide.Columns > wide.Rows);
        Assert.True(tall.Rows > tall.Columns);

        foreach (var (plan, w, h) in new[] { (wide, 448d, 128d), (tall, 168d, 368d) })
        {
            Assert.True(plan.Columns * plan.Rows >= 365);
            Assert.True(plan.Width <= w + 1e-6 && plan.Height <= h + 1e-6);
            Assert.True(plan.Diameter < plan.Pitch);
        }

        var month = DotGrid.Fit(31, 1000, 100, maxColumns: 7);
        Assert.Equal(7, month.Columns);
        Assert.Equal((2, 4), month.Cell(31 - 1));
        Assert.Equal(0, DotGrid.Fit(0, 100, 100).Columns);
        Assert.Equal(0, DotGrid.Fit(10, 0, 100).Pitch);
    }
}
