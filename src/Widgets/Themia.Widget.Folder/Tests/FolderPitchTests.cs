using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>
/// [S-12451] [S-12807] The folder's list rows fill the items' room: as many as the room holds at their optimal (three
/// cells, a row's 28), sharing it evenly, touching, nothing left over at the foot.
/// </summary>
public sealed class FolderPitchTests
{
    [Theory]
    [InlineData(224, 32, 7)]               // seven rows of three cells hold, never eight squeezed: 32 each
    [InlineData(112, 112.0 / 3, 3)]        // three rows, a third each
    [InlineData(57, 57, 1)]                // one row, the whole room
    [InlineData(41, 41, 1)]
    public void S12451_a_list_row_fills_the_items_room(double height, double pitch, int shown)
    {
        var reach = FolderLayout.Reach(height, ItemGrid.Default());
        Assert.Equal(pitch, reach.Pitch, 6);
        Assert.Equal(shown, reach.Shown);
        Assert.Equal(height, reach.Room, 6);
    }
}
