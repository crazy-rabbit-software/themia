using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-12451] [S-12807] The folder's list rows fill the items' room: whole rows of 28 grown until they touch.</summary>
public sealed class FolderPitchTests
{
    [Theory]
    [InlineData(224, 28)]   // the natural list: 8 rows of 28
    [InlineData(112, 28)]   // four whole rows
    [InlineData(57, 28)]    // two whole rows, a pixel over: the rest under them
    [InlineData(41, 41)]    // one row in a 41 box: it takes the box
    public void S12451_a_list_row_fills_the_items_room(double height, double pitch)
    {
        var reach = FolderLayout.Reach(height);
        Assert.Equal(pitch, reach.Pitch);
        Assert.True(reach.Length(FolderLayout.Rows(height)) <= height + 1e-9);
    }
}
