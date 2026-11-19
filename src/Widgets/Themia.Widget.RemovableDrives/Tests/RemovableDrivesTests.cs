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
    public void S11027_its_height_follows_its_rows_and_a_dragged_edge_sets_them()
    {
        Assert.Equal(4 * 40, RemovableDrivesPlan.Reach(100, 4).Length(4));
        var sizing = Kind.SizingOf();
        Assert.Equal(12 + (4 * 40) + 12, sizing.Natural.Height);
        var two = Assert.IsType<RemovableDrivesConfig>(Kind.SizeSets!(new RemovableDrivesConfig(), new SizeDip(300, 12 + 80 + 12)));
        Assert.Equal(2, two.Rows);
        Assert.Equal(6, ((RemovableDrivesConfig)new RemovableDrivesConfig { Rows = 6 }.Clone()).Rows);
    }

    [Fact]
    public void S12452_its_rows_reach_the_top_and_the_foots_inset_and_never_pass_half_again()
    {
        // The view at the natural box: four rows of 40 and the foot's 12. The texts span 172 - 12 = 160, of
        // which four texts take 144: their bands share what is left, the first one's over the top.
        var natural = RemovableDrivesPlan.Reach((4 * 40) + 12, 4);
        Assert.Equal((40d, 2d, 1d), (natural.Pitch, natural.Over, natural.Gap));
        Assert.Equal(60, RemovableDrivesPlan.Reach(900, 4).Pitch);
        Assert.Equal((40d, 2d), (RemovableDrivesPlan.Reach(100, 4).Pitch, RemovableDrivesPlan.Reach(100, 4).Over));
    }
}
