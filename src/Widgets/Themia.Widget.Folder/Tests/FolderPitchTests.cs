using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-12451] [S-12807] The folder's list rows reach the items' top and the foot's inset with their ink: whole rows of 28 stretched over what is left, at most half again.</summary>
public sealed class FolderPitchTests
{
    [Theory]
    [InlineData(224)]    // the natural list: 8 rows
    [InlineData(112)]    // four whole rows
    [InlineData(57)]     // two whole rows
    public void S12451_a_list_row_reaches_the_items_ends_up_to_half_again(double height)
    {
        var reach = FolderLayout.Reach(height);
        var rows = FolderLayout.Rows(height);
        var ink = LineInk.Row(1, FolderLayout.Lead);
        var halo = (reach.Pitch - 20) / 2;

        // The first row's ink (its icon) on the items' top, the last one's on their end; no box past the inset.
        Assert.Equal(0, -reach.Over + halo + ink.Over, 6);
        Assert.Equal(height, reach.Length(rows) - reach.Over - reach.Under, 6);
        Assert.InRange(reach.Pitch, 20, 42);
        Assert.True(reach.Over <= 12 + 1e-9 && reach.Under <= 12 + 1e-9);
    }

    [Fact]
    public void S12451_one_row_is_centred_round_its_ink()
    {
        var reach = FolderLayout.Reach(41);
        var ink = LineInk.Row(1, FolderLayout.Lead);
        var top = -reach.Over + ((reach.Pitch - 20) / 2) + ink.Over;
        var foot = 41 - (top + 20 - ink.Over - ink.Under);
        Assert.InRange(foot - top, 0, 1);
    }
}
