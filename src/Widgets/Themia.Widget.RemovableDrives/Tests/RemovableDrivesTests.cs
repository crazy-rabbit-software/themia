using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RemovableDrives.Tests;

/// <summary>[S-4662] [S-11027] The devices' rows: two lines, a picture and a bar at every width; the list's rows its setting.</summary>
public sealed class RemovableDrivesTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("removableDrives"));

    [Fact]
    public void S11027_a_row_has_its_two_lines_its_picture_and_its_bar_at_every_width()
    {
        Assert.Equal(new RemovableDrivesLook(2, 32, 62), RemovableDrivesPlan.Of(220));
        Assert.Equal(new RemovableDrivesLook(2, 32, 88), RemovableDrivesPlan.Of(300));
        Assert.Equal(new RemovableDrivesLook(2, 32, 140), RemovableDrivesPlan.Of(520));
    }

    [Fact]
    public void S11027_S14500_its_rows_are_a_new_cards_height_every_cell_from_one_row_up()
    {
        Assert.Equal(4 * 40, RemovableDrivesPlan.Reach((4 * 40) - 12, ItemGrid.Default()).Start(4));
        var sizing = Kind.SizingOf();
        Assert.Equal(4 * 40, sizing.Natural.Height);
        Assert.Equal(40, sizing.Min.Height);
        Assert.Equal(6, ((RemovableDrivesConfig)new RemovableDrivesConfig { Rows = 6 }.Clone()).Rows);
    }

    [Fact]
    public void S12452_S14810_its_rows_keep_their_size_touching_from_the_cards_top_edge()
    {
        // [S-14810] The view at a box: rows of four cells from the card's top edge, as many as it reaches, the last cut.
        static (double, int, double) Of(RowReach r) => (r.Pitch, r.Shown, r.Room);
        Assert.Equal((40.0, 5, 184.0), Of(RemovableDrivesPlan.Reach((4 * 40) + 12, ItemGrid.Default())));
        Assert.Equal((40.0, 23, 912.0), Of(RemovableDrivesPlan.Reach(900, ItemGrid.Default())));
        Assert.Equal((40.0, 3, 112.0), Of(RemovableDrivesPlan.Reach(100, ItemGrid.Default())));
    }
}
