using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-12451] [S-12410] The folder's list rows run down to the card's edge: whole rows of 28 stretched over what is left, at most half again.</summary>
public sealed class FolderPitchTests
{
    [Theory]
    [InlineData(236, 29)]   // the natural list: 8 rows, the foot's 12 shared
    [InlineData(112, 28)]   // whole rows already
    [InlineData(41, 41)]    // one row in a 41 box: stretched, under half again
    [InlineData(55, 42)]    // one whole row: at most half again, the rest is room under it
    [InlineData(57, 28)]    // two whole rows: half a pixel each is no whole pixel, they stay 28
    public void S12451_a_list_row_fills_the_list_to_the_edge_up_to_half_again(double height, double pitch)
    {
        Assert.Equal(pitch, FolderLayout.Pitch(height));
        Assert.True(FolderLayout.Rows(height) * FolderLayout.Pitch(height) <= height);
    }

    [Fact]
    public void S12451_the_list_reaches_into_the_foot_s_inset_and_the_cells_keep_it()
    {
        var area = new WidgetArea(300, 236);
        Assert.Equal(FolderLayout.ItemsHeight(area, false, false, false) + 12, FolderLayout.ListHeight(area, false, false, false));
    }
}
