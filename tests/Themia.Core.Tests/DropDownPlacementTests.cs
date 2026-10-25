using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3522] Where the settings menu stands under ⚙: level with the button, under the bar, inside the work area, clear of the view bar.</summary>
public sealed class DropDownPlacementTests
{
    private static readonly PixelRect Area = new(0, 0, 1280, 800);
    private static readonly PixelRect Bar = new(440, 8, 400, 40);
    private static readonly PixelRect Gear = new(764, 12, 32, 32);

    [Fact]
    public void S3522_the_menu_hangs_under_the_bar_its_start_edge_level_with_the_button()
    {
        var (x, y) = DropDownPlacement.Place(Gear, Bar, 300, 400, Area, [], rightToLeft: false);

        Assert.Equal(Gear.X, x);
        Assert.Equal(Bar.Y + Bar.Height + DropDownPlacement.Gap, y);
    }

    [Fact]
    public void S3522_right_to_left_its_end_edge_is_level_with_the_buttons()
    {
        var (x, _) = DropDownPlacement.Place(Gear, Bar, 300, 400, Area, [], rightToLeft: true);

        Assert.Equal(Gear.X + Gear.Width - 300, x);
    }

    [Fact]
    public void S3522_it_slides_along_to_stay_inside_the_work_area()
    {
        var nearEdge = new PixelRect(1240, 12, 32, 32);
        Assert.Equal(Area.Width - 300, DropDownPlacement.Place(nearEdge, Bar, 300, 400, Area, [], rightToLeft: false).X);

        var nearStart = new PixelRect(10, 12, 32, 32);
        Assert.Equal(0, DropDownPlacement.Place(nearStart, Bar, 300, 400, Area, [], rightToLeft: true).X);
    }

    [Fact]
    public void S3522_it_steps_below_the_view_bar_where_it_would_cover_it()
    {
        var viewBar = new PixelRect(700, 52, 300, 36);
        var (_, y) = DropDownPlacement.Place(Gear, Bar, 300, 400, Area, [Bar, viewBar], rightToLeft: false);

        Assert.Equal(viewBar.Y + viewBar.Height + DropDownPlacement.Gap, y);
    }

    [Fact]
    public void S3522_a_view_bar_beside_it_moves_nothing()
    {
        var viewBar = new PixelRect(100, 52, 300, 36);
        var (_, y) = DropDownPlacement.Place(Gear, Bar, 300, 400, Area, [viewBar], rightToLeft: false);

        Assert.Equal(Bar.Y + Bar.Height + DropDownPlacement.Gap, y);
    }

    [Fact]
    public void S3522_it_never_leaves_the_work_area_whatever_it_covers()
    {
        var short_ = new PixelRect(0, 0, 1280, 300);
        var viewBar = new PixelRect(700, 52, 300, 36);
        var (_, y) = DropDownPlacement.Place(Gear, Bar, 300, 260, short_, [viewBar], rightToLeft: false);

        Assert.True(y + 260 <= short_.Height);
        Assert.True(y >= short_.Y);
    }
}
