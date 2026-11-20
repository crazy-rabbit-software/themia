using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>
/// [S-12451] [S-12807] [S-14810] The folder's list rows keep their one size in the items' room (three cells, a row's 28),
/// touching from its top, as many as the room reaches, the last cut by its end.
/// </summary>
public sealed class FolderPitchTests
{
    [Theory]
    [InlineData(224, 8)]  // seven rows of three cells whole, the eighth cut
    [InlineData(112, 4)]
    [InlineData(57, 2)]
    [InlineData(30, 1)]
    public void S12451_S14810_a_list_row_keeps_its_size_in_any_room(double height, int shown)
    {
        var reach = FolderLayout.Reach(height, ItemGrid.Default());
        Assert.Equal(30, reach.Pitch, 6);
        Assert.Equal(shown, reach.Shown);
        Assert.Equal(height, reach.Room, 6);
    }
}
