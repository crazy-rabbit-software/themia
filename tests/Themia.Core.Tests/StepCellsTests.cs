using Themia.Core.Geometry;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14269] A size step in cells is exactly what the host lays, axis by axis (the owner's WR-6: "they should be designed so
/// that they are 3 blocks high or wide"): a sizing's item and parts are counted in whole cells of each axis's own pitch (the
/// screen's DIPs along it over its lines) by the host's one count, the one the host lays a box of that length in
/// (<see cref="GridFit"/>: a desktop icon's box, a fence's). Before, both axes were counted in the columns' pitch with a
/// fiftieth of a cell's slack, so a step and the host's box parted on displays whose pitches differ.
/// </summary>
public sealed class StepCellsTests
{
    /// <summary>The cells the host lays a box of <paramref name="box"/> DIPs in on <paramref name="screen"/>: its fixed sizing on the grid.</summary>
    private static (int Cols, int Rows) Host(ScreenMetrics screen, SizeDip box)
    {
        var limits = SizeFit.Limits(WidgetSizing.Fixed(box), screen, 1)!;
        var (cols, rows) = GridGeometry.GridDims(screen);
        return ((int)Math.Round(limits.Width.Min * cols), (int)Math.Round(limits.Height.Min * rows));
    }

    /// <summary>The cells one step of an item <paramref name="item"/> DIPs long takes on <paramref name="screen"/>, across and down.</summary>
    private static (int Cols, int Rows) Step(ScreenMetrics screen, SizeDip item)
    {
        var sizing = WidgetSizing.Range(new SizeDip(1, 1), new SizeDip(1, 1), new SizeDip(4000, 4000))
            .WithWidthSteps(SizeSteps.InCells(item.Width, 1, 10))
            .WithHeightSteps(SizeSteps.InCells(item.Height, 1, 10));
        var limits = SizeFit.Limits(sizing, screen, 1)!;
        var (cols, rows) = GridGeometry.GridDims(screen);
        return ((int)Math.Round(limits.Width.Step * cols), (int)Math.Round(limits.Height.Step * rows));
    }

    /// <summary>
    /// Displays: the owner's 2560 × 1440 at 100 % and 150 %, 1080p, 1080p at 125 % (9.974 across, 10.047 down), 1366 × 768,
    /// 4:3 at 1024 × 768 (10.039 across, 9.974 down), 16:10 at 125 % (1536 × 960: 9.974 across, 10 down), 5:4, a 3:2 laptop
    /// at 200 % and 2880 × 1800 at 175 %.
    /// </summary>
    public static TheoryData<double, double> Displays => new()
    {
        { 2560, 1440 },
        { 2560 / 1.5, 1440 / 1.5 },
        { 1920, 1080 },
        { 1536, 864 },
        { 1366, 768 },
        { 1024, 768 },
        { 1536, 960 },
        { 1280, 1024 },
        { 1368, 912 },
        { 2880 / 1.75, 1800 / 1.75 },
    };

    /// <summary>The lengths the kinds' steps hold: rows of one, two and three lines, a table's titles, icon boxes at every size, a week's day.</summary>
    private static readonly double[] Lengths = [28, 36, 40, 56, 60, 76, 84, 100, 124, 148, 162, 200, 260.0 / 7];

    [Fact]
    public void S14269_on_1536x864_a_box_100_tall_is_ten_rows_for_the_host_and_for_its_step()
    {
        var screen = new ScreenMetrics(1536, 864);

        Assert.Equal(10, Host(screen, new SizeDip(76, 100)).Rows);
        Assert.Equal(10, Step(screen, new SizeDip(76, 100)).Rows);
    }

    [Fact]
    public void S14269_on_1366x768_a_small_icon_60_across_is_seven_columns_for_the_host_and_for_its_step()
    {
        var screen = new ScreenMetrics(1366, 768);

        Assert.Equal(7, Host(screen, new SizeDip(60, 84)).Cols);
        Assert.Equal(7, Step(screen, new SizeDip(60, 84)).Cols);
    }

    [Theory]
    [MemberData(nameof(Displays))]
    public void S14269_a_step_in_cells_is_the_hosts_box_of_its_length_on_each_axis(double width, double height)
    {
        var screen = new ScreenMetrics(width, height);
        foreach (var length in Lengths)
        {
            var box = new SizeDip(length, length);
            Assert.True(Host(screen, box) == Step(screen, box), $"{length:0.##} on {width:0.##}x{height:0.##}: host {Host(screen, box)}, step {Step(screen, box)}");
        }
    }

    [Theory]
    [InlineData(1.0, 3, 4, 6, 4, 20)]
    [InlineData(1.5, 3, 4, 6, 4, 21)]
    public void S14269_on_the_owners_displays_the_steps_are_what_they_were(double scale, int row1, int row2, int row3, int titles, int day)
    {
        // 2560 × 1440 at 100 % (10 across and down) and at 150 % (9.98 across, 10 down): rows, titles and a week's day as before.
        var screen = new ScreenMetrics(2560 / scale, 1440 / scale);

        Assert.Equal(row1, Step(screen, new SizeDip(1, 28)).Rows);
        Assert.Equal(row2, Step(screen, new SizeDip(1, 40)).Rows);
        Assert.Equal(row3, Step(screen, new SizeDip(1, 56)).Rows);
        Assert.Equal(titles, Step(screen, new SizeDip(1, 36)).Rows);
        Assert.Equal(day, Step(screen, new SizeDip(200, 1)).Cols);
    }

    [Fact]
    public void S14269_where_the_pitches_differ_each_axis_counts_in_its_own()
    {
        // 4:3 at 1024 × 768: 10.039 across, 9.974 down. A row of two lines (40) is 4.01 rows: five, as the host lays a box of
        // 40; across, 40 is 3.98 columns: four.
        var screen = new ScreenMetrics(1024, 768);

        Assert.Equal((4, 5), Step(screen, new SizeDip(40, 40)));
        Assert.Equal(Host(screen, new SizeDip(40, 40)), Step(screen, new SizeDip(40, 40)));
    }
}
