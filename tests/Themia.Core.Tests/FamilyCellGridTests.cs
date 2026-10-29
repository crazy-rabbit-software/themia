using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6713] The cells' grid (Shortcuts', a folder's grid and icons): columns, rows, the marks around them.</summary>
public sealed class FamilyCellGridTests
{
    [Fact]
    public void S6713_a_named_cell_is_its_icon_and_16_either_side_an_unnamed_one_its_icon()
    {
        Assert.Equal(64, RowRules.CellLeast(32, named: true));
        Assert.Equal(80, RowRules.CellLeast(48, named: true));
        Assert.Equal(32, RowRules.CellLeast(32, named: false));
    }

    [Theory]
    [InlineData(168, 2, 78)]   // Tall: two named columns of 78
    [InlineData(88, 1, 88)]    // XS: one
    [InlineData(288, 3, 88)]   // M: three
    [InlineData(140, 2, 64)]   // exactly two at the least
    [InlineData(139.5, 1, 139.5)]
    public void S6713_cells_share_the_width_out_12_apart(double width, int columns, double cell)
    {
        var grid = RowRules.Cells(width, 400, 32, named: true);
        Assert.Equal(columns, grid.Columns);
        Assert.Equal(cell, grid.Width, 3);
        Assert.Equal(width, (columns * grid.Width) + ((columns - 1) * FamilySpace.S3), 3);
    }

    [Theory]
    [InlineData(60, 1)]    // a named 32 cell is 52 tall
    [InlineData(115, 1)]
    [InlineData(116, 2)]   // 52 + 12 + 52
    [InlineData(40, 0)]
    public void S6713_only_whole_rows_of_cells_count(double height, int rows)
        => Assert.Equal(rows, RowRules.Cells(200, height, 32, named: true).Rows);

    [Fact]
    public void S6713_marks_stand_4_around_the_cell_and_4_apart_so_the_cells_are_12_apart()
    {
        var grid = RowRules.Cells(288, 200, 32, named: true);
        Assert.Equal(grid.Width + 8, grid.MarkWidth);
        Assert.Equal(52 + 8, grid.MarkHeight);
        Assert.Equal(grid.Width + FamilySpace.S3, grid.PitchX);
        Assert.Equal(52 + FamilySpace.S3, grid.PitchY);
        Assert.Equal(grid.Columns * grid.Rows, grid.Visible);
    }

    [Fact]
    public void S6713_a_box_too_narrow_for_one_cell_still_has_one_column_at_the_least()
    {
        var grid = RowRules.Cells(20, 100, 48, named: false);
        Assert.Equal(1, grid.Columns);
        Assert.Equal(48, grid.Width);
    }
}
