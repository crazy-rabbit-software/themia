using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-12451] [S-12807] The folder's list rows reach the items' top and the foot's inset: whole rows of 28 stretched over what is left, at most half again.</summary>
public sealed class FolderPitchTests
{
    [Theory]
    [InlineData(224, 28, 4)]    // the natural list: 8 rows; 9 between two texts, a band of 4 each side and 1 over
    [InlineData(112, 30, 5)]    // four whole rows: their ends' bands go between them
    [InlineData(41, 40, 0)]     // one row in a 41 box: centred, stretched round its text
    [InlineData(57, 36, 8)]     // two whole rows: 17 between their texts, 8 a side
    public void S12451_a_list_row_reaches_the_items_ends_up_to_half_again(double height, double pitch, double over)
    {
        var reach = FolderLayout.Reach(height);
        Assert.Equal((pitch, over), (reach.Pitch, reach.Over));
        Assert.True(reach.Length(FolderLayout.Rows(height)) - (2 * reach.Over) <= height + 1e-9);
    }
}
