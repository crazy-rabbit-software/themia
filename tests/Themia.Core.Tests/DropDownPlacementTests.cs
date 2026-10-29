using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3522] Where the settings menu stands under ⚙: centred under its button, under the bar, inside the work area, clear of the view bar.</summary>
public sealed class DropDownPlacementTests
{
    private static readonly PixelRect Area = new(0, 0, 1280, 800);
    private static readonly PixelRect Bar = new(440, 8, 400, 40);
    private static readonly PixelRect Gear = new(764, 12, 32, 32);

    [Fact]
    public void S3522_the_menu_hangs_under_the_bar_centred_under_its_button()
    {
        var (x, y) = DropDownPlacement.Place(Gear, Bar, 300, 400, Area, []);

        Assert.Equal(Gear.X + (Gear.Width / 2) - 150, x);
        Assert.Equal(Bar.Y + Bar.Height + DropDownPlacement.Gap, y);
    }

    [Fact]
    public void S3522_it_slides_along_to_stay_inside_the_work_area()
    {
        var nearEdge = new PixelRect(1240, 12, 32, 32);
        Assert.Equal(Area.Width - 300, DropDownPlacement.Place(nearEdge, Bar, 300, 400, Area, []).X);

        var nearStart = new PixelRect(10, 12, 32, 32);
        Assert.Equal(0, DropDownPlacement.Place(nearStart, Bar, 300, 400, Area, []).X);
    }

    [Fact]
    public void S3522_it_steps_below_the_view_bar_where_it_would_cover_it()
    {
        var viewBar = new PixelRect(700, 52, 300, 36);
        var (_, y) = DropDownPlacement.Place(Gear, Bar, 300, 400, Area, [Bar, viewBar]);

        Assert.Equal(viewBar.Y + viewBar.Height + DropDownPlacement.Gap, y);
    }

    [Fact]
    public void S3522_a_view_bar_beside_it_moves_nothing()
    {
        var viewBar = new PixelRect(100, 52, 300, 36);
        var (_, y) = DropDownPlacement.Place(Gear, Bar, 300, 400, Area, [viewBar]);

        Assert.Equal(Bar.Y + Bar.Height + DropDownPlacement.Gap, y);
    }

    [Fact]
    public void S3522_it_never_leaves_the_work_area_whatever_it_covers()
    {
        var short_ = new PixelRect(0, 0, 1280, 300);
        var viewBar = new PixelRect(700, 52, 300, 36);
        var (_, y) = DropDownPlacement.Place(Gear, Bar, 300, 260, short_, [viewBar]);

        Assert.True(y + 260 <= short_.Height);
        Assert.True(y >= short_.Y);
    }

    [Fact]
    public void S3540_a_long_menu_may_be_as_tall_as_the_work_area_under_the_bar_less_a_gap()
    {
        Assert.Equal(Area.Height - (Bar.Y + Bar.Height + DropDownPlacement.Gap) - DropDownPlacement.Gap, DropDownPlacement.Room(Bar, Area));

        // A menu of that height hangs under the bar and ends inside the work area.
        var room = DropDownPlacement.Room(Bar, Area);
        var (_, y) = DropDownPlacement.Place(Gear, Bar, 300, room, Area, []);
        Assert.True(y + room <= Area.Y + Area.Height);

        Assert.Equal(0, DropDownPlacement.Room(new PixelRect(0, 790, 100, 40), Area));
    }
}
