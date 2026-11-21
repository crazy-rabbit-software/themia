using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6713] [S-14816] The cells' grid (a folder's grid and icons): one box from the card's edges, columns, lines.</summary>
public sealed class FamilyCellGridTests
{
    [Fact]
    public void S6713_a_named_cell_is_its_icon_and_16_either_side_an_unnamed_one_its_icon()
    {
        Assert.Equal(64, RowRules.CellLeast(32, named: true));
        Assert.Equal(80, RowRules.CellLeast(48, named: true));
        Assert.Equal(32, RowRules.CellLeast(32, named: false));
    }

    /// <summary>
    /// [S-14816] Owner, 2026-11-21: "the weather forecast widget grid view items do not go from edge to edge, they have space
    /// everywhere. ... fix this, also for other widgets if applicable." A cell's box is its lead and name with 8 round them,
    /// in whole grid cells, at every width: every column the width reaches, touching, from the card's left edge, the last cut
    /// by its right edge.
    /// </summary>
    [Theory]
    [InlineData(168, 3)]
    [InlineData(80, 1)]
    [InlineData(240, 3)]
    [InlineData(239, 3)]
    [InlineData(20, 1)]
    public void S14816_cells_keep_one_box_and_take_every_column_the_width_reaches(double width, int columns)
    {
        var grid = RowRules.Cells(width, 400, 32, named: true, Grid);
        Assert.Equal(columns, grid.Columns);
        Assert.Equal(80, grid.Width);
        Assert.Equal(70, grid.Height);
        Assert.Equal(0, grid.Left(0));
        Assert.Equal(80, grid.Left(1));
    }

    [Theory]
    [InlineData(70, 1)]
    [InlineData(71, 2)]
    [InlineData(140, 2)]
    [InlineData(141, 3)]
    [InlineData(10, 1)]
    public void S14816_every_line_the_height_reaches_counts_the_last_cut(double height, int rows)
        => Assert.Equal(rows, RowRules.Cells(200, height, 32, named: true, Grid).Rows);

    [Fact]
    public void S14816_an_unnamed_cell_is_its_icon_with_8_round_it_and_its_edges_are_whole_pixels()
    {
        Assert.Equal((50.0, 50.0), (RowRules.Cells(200, 200, 32, named: false, Grid).Width, RowRules.Cells(200, 200, 32, named: false, Grid).Height));
        var scaled = RowRules.Cells(300, 200, 32, named: true, new ItemGrid(9.974, 1.5));
        Assert.All(Enumerable.Range(0, 4), c => Assert.Equal(Math.Round(scaled.Left(c) * 1.5), scaled.Left(c) * 1.5, 6));
        Assert.Equal(scaled.Columns * scaled.Rows, scaled.Visible);
    }

    private static readonly ItemGrid Grid = ItemGrid.Default();
}
