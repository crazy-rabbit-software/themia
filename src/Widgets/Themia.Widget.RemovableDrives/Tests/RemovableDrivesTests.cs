using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RemovableDrives.Tests;

/// <summary>[S-4662] The devices' rows on the family's Row block: their lines, lead and bar by the tier.</summary>
public sealed class RemovableDrivesTests
{
    // The view's area: the card less its header (36 of a card's height).
    private static WidgetArea Area(double width, double cardHeight) => new(width, cardHeight - FamilySpace.ContentTop);

    [Fact]
    public void S4662_a_row_grows_lines_by_the_tier_its_picture_from_180_and_its_bar_from_a_content_of_240()
    {
        Assert.Equal(new RemovableDrivesLook(1, 0, 0), RemovableDrivesPlan.Of(Area(120, 120)));
        Assert.Equal(new RemovableDrivesLook(1, 32, 0), RemovableDrivesPlan.Of(Area(200, 150)));
        Assert.Equal(new RemovableDrivesLook(2, 32, 94), RemovableDrivesPlan.Of(Area(320, 240)));
        Assert.Equal(new RemovableDrivesLook(1, 32, 140), RemovableDrivesPlan.Of(Area(480, 160)));
        Assert.Equal(new RemovableDrivesLook(2, 32, 0), RemovableDrivesPlan.Of(Area(200, 400)));
        Assert.Equal(new RemovableDrivesLook(3, 32, 140), RemovableDrivesPlan.Of(Area(800, 560)));
    }

    [Fact]
    public void S4662_the_list_shows_whole_rows_and_at_least_one()
    {
        Assert.Equal(2 * 28, RemovableDrivesPlan.ListHeight(Area(120, 120), 4, 1));
        Assert.Equal(4 * 40, RemovableDrivesPlan.ListHeight(Area(320, 240), 4, 2));
        Assert.Equal(40, RemovableDrivesPlan.ListHeight(Area(320, 240), 1, 2));
        Assert.Equal(0, RemovableDrivesPlan.ListHeight(Area(320, 240), 0, 2));
        Assert.Equal(28, RemovableDrivesPlan.ListHeight(new WidgetArea(200, 20), 3, 1));
    }
}
