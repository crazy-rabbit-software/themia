using Themia.Core.Geometry;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2325]..[S-2327] [S-11707] [S-13715] [S-14910] The edit bar's one open flyout, as one value.</summary>
public sealed class OpenFlyoutTests
{
    [Fact]
    public void S2327_one_flyout_at_a_time_says_which_entry_is_open()
    {
        Assert.False(OpenFlyout.None.IsOpen);
        Assert.Null(OpenFlyout.None.PageId);

        var views = OpenFlyout.Of(SettingsPageId.Views);
        Assert.True(views.IsOpen);
        Assert.Equal(SettingsPageId.Views, views.PageId);
        Assert.True(views.HoldsPage);
        Assert.False(views.IsSelection || views.IsAdd || views.IsGallery);
        Assert.Equal(views, OpenFlyout.Of(SettingsPageId.Views));
        Assert.NotEqual(views, OpenFlyout.Of(SettingsPageId.General));

        Assert.True(OpenFlyout.Selection.IsSelection);
        Assert.True(OpenFlyout.Selection.HoldsPage);
        Assert.Null(OpenFlyout.Selection.PageId);
        Assert.True(OpenFlyout.Gallery.IsGallery);
        Assert.False(OpenFlyout.Gallery.HoldsPage);
    }

    [Fact]
    public void S13715_the_unplaced_icons_page_is_its_own_flyout_that_stays_open()
    {
        var unplaced = OpenFlyout.Of(SettingsPageId.DesktopItems);

        Assert.Same(OpenFlyout.Unplaced, unplaced);
        Assert.Equal(SettingsPageId.DesktopItems, unplaced.PageId);
        Assert.True(unplaced.StaysOpen);
        Assert.False(OpenFlyout.Of(SettingsPageId.Appearance).StaysOpen);
        Assert.False(OpenFlyout.Selection.StaysOpen);
    }

    [Fact]
    public void S13018_the_add_page_carries_the_area_it_places_into()
    {
        var area = new FracRect(0.1, 0.2, 0.3, 0.4);

        Assert.True(OpenFlyout.Adding().IsAdd);
        Assert.Null(OpenFlyout.Adding().AddArea);
        Assert.Equal(area, OpenFlyout.Adding(area).AddArea);
        Assert.Null(OpenFlyout.Selection.AddArea);
        Assert.False(OpenFlyout.Adding(area).HoldsPage);
    }
}
