using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>
/// [S-12451] [S-12807] [S-12830] The folder's list rows fill the items' room: whole grid cells (three at least, a row's
/// 28), as many as the room holds at that height, touching, the cells left over at the foot.
/// </summary>
public sealed class FolderPitchTests
{
    [Theory]
    [InlineData(224, 30, 7)]   // 22 cells: seven rows of three, never eight squeezed
    [InlineData(112, 30, 3)]   // 11 cells: three rows of three
    [InlineData(57, 50, 1)]    // 5 cells: one row, five cells
    [InlineData(41, 40, 1)]    // one row in a 41 box: its four whole cells
    public void S12451_a_list_row_fills_the_items_room(double height, double pitch, int shown)
    {
        var reach = FolderLayout.Reach(height, ItemGrid.Default());
        Assert.Equal(pitch, reach.Pitch, 6);
        Assert.Equal(shown, reach.Shown);
        Assert.True(reach.Length(FolderLayout.Rows(height)) <= height + 1e-9);
    }
}
