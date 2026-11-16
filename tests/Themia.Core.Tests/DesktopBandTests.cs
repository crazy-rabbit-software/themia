using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5544] The desktop's band is one rectangle in screen space: it runs on from the display it
/// started on onto the others, every display draws its part, and it selects the icons it touches on
/// any of them. Two displays side by side, the left at 100 % with Karpo's strip along its left edge,
/// the right at 150 % with a taskbar along its bottom.
/// </summary>
public sealed class DesktopBandTests
{
    private static readonly PixelRect Left = new(0, 0, 1920, 1080);
    private static readonly PixelRect LeftWork = new(64, 0, 1856, 1080);
    private static readonly PixelRect Right = new(1920, 0, 3840, 2160);
    private static readonly PixelRect RightWork = new(1920, 0, 3840, 2088);
    private static readonly PixelRect[] Areas = [LeftWork, RightWork];

    [Fact]
    public void S5544_a_point_in_a_surfaces_dips_is_a_screen_point_at_that_surfaces_scale()
    {
        Assert.Equal((300d, 200d), DesktopBand.ToScreen(Left, 1.0, 300, 200));
        Assert.Equal((1920 + 450d, 300d), DesktopBand.ToScreen(Right, 1.5, 300, 200));
    }

    /// <summary>The pointer is captured by the surface the press landed on, so past its edge its DIPs run on: onto the neighbour, in pixels.</summary>
    [Fact]
    public void S5544_a_captured_pointer_past_the_surfaces_edge_lands_on_the_neighbouring_display()
    {
        Assert.Equal((2420d, 400d), DesktopBand.ToScreen(Left, 1.0, 2420, 400));
        Assert.Equal((1920 - 150d, 600d), DesktopBand.ToScreen(Right, 1.5, -100, 400));
    }

    [Fact]
    public void S5544_the_band_grows_from_one_display_onto_the_next()
    {
        var band = DesktopBand.Of(Areas, (500, 300), (2600, 900));

        Assert.Equal(new BandRect(500, 300, 2600, 900), band);
        Assert.NotNull(DesktopBand.On(band, Left, LeftWork, 1.0));
        Assert.NotNull(DesktopBand.On(band, Right, RightWork, 1.5));
    }

    /// <summary>The band stops at what a dock or the taskbar reserved, and at the outer edge of the displays.</summary>
    [Fact]
    public void S5544_the_band_stays_inside_the_work_areas_taken_together()
    {
        Assert.Equal(new BandRect(500, 300, 64, 0), DesktopBand.Of(Areas, (500, 300), (10, -50)));
        Assert.Equal(new BandRect(500, 300, 5760, 2088), DesktopBand.Of(Areas, (500, 300), (9000, 2150)));
    }

    /// <summary>Below the smaller display there is no desktop: the band is held at that display's edge, the nearest point of any work area.</summary>
    [Fact]
    public void S5544_a_pointer_off_every_display_holds_the_band_at_the_nearest_work_area()
        => Assert.Equal((1000d, 1080d), DesktopBand.Into(Areas, 1000, 1500));

    [Fact]
    public void S5544_displays_that_do_not_touch_still_share_one_band()
    {
        PixelRect[] apart = [new(0, 0, 1000, 1000), new(1400, 0, 1000, 1000)];

        // In the gap the pointer is held at the nearer display's edge; past the gap it is on the other.
        Assert.Equal((1000d, 500d), DesktopBand.Into(apart, 1100, 500));
        Assert.Equal((1400d, 500d), DesktopBand.Into(apart, 1350, 500));
        Assert.Equal(new BandRect(200, 200, 1800, 700), DesktopBand.Of(apart, (200, 200), (1800, 700)));
    }

    [Fact]
    public void S5544_no_usable_work_area_leaves_the_point_as_it_is()
        => Assert.Equal((5d, 7d), DesktopBand.Into([new PixelRect(0, 0, 0, 0)], 5, 7));

    /// <summary>[S-5563] Each display draws the band in its own DIPs: the same screen rectangle, at 100 % and at 150 %.</summary>
    [Fact]
    public void S5544_each_display_draws_the_band_in_its_own_dips()
    {
        var band = new BandRect(500, 300, 2520, 900);

        Assert.Equal(new BandRect(500, 300, 2520, 900), DesktopBand.On(band, Left, LeftWork, 1.0));
        Assert.Equal(new BandRect((500 - 1920) / 1.5, 200, 400, 600), DesktopBand.On(band, Right, RightWork, 1.5));
    }

    /// <summary>A display on the left or above has a negative corner; the band's DIPs are still measured from it.</summary>
    [Fact]
    public void S5544_a_display_with_a_negative_corner_draws_from_its_own_corner()
    {
        var second = new PixelRect(-2560, -200, 2560, 1440);

        Assert.Equal(new BandRect(2460, 300, 2560 + 100, 500), DesktopBand.On(new BandRect(-100, 100, 100, 300), second, second, 1.0));
    }

    [Fact]
    public void S5544_a_display_the_band_does_not_reach_draws_nothing()
    {
        Assert.Null(DesktopBand.On(new BandRect(500, 300, 900, 600), Right, RightWork, 1.5));
        Assert.Null(DesktopBand.On(new BandRect(2000, 2100, 2500, 2150), Right, new PixelRect(1920, 0, 3840, 2088), 0));
    }

    /// <summary>A band wholly inside what a display's taskbar reserved cannot exist (it is clamped), and one outside the work area draws nothing there.</summary>
    [Fact]
    public void S5544_a_band_outside_a_displays_work_area_draws_nothing_on_it()
        => Assert.Null(DesktopBand.On(new BandRect(2000, 2100, 2500, 2150), Right, RightWork, 1.5));

    /// <summary>
    /// [S-6624] [S-5540] The icons' boxes are the one selection's, in screen pixels on every display,
    /// so one band selects on both and no scale is involved: the icon on the 150 % display is hit by
    /// where it is on screen.
    /// </summary>
    [Fact]
    public void S5544_the_band_selects_the_icons_it_touches_on_every_display()
    {
        DesktopBox[] boxes =
        [
            new("a", 0, 100, 100, 80, 100),
            new("b", 0, 1700, 400, 80, 100),
            new("c", 1, 2000, 450, 120, 150),
            new("d", 1, 4000, 1500, 120, 150),
            new("empty", 1, 2100, 500, 0, 0),
        ];

        Assert.Equal(["b", "c"], DesktopBand.Hits(new BandRect(1750, 420, 2050, 480), boxes));
        Assert.Equal(["a", "b", "c", "d"], DesktopBand.Hits(DesktopBand.Of(Areas, (70, 10), (5000, 2000)), boxes));
        Assert.Empty(DesktopBand.Hits(new BandRect(300, 300, 900, 380), boxes));
    }

    /// <summary>[S-6625] The band's keys go to the one selection with the same rules as before: replace, Shift adds, Ctrl toggles, live.</summary>
    [Fact]
    public void S5544_a_band_across_displays_combines_with_the_selection_as_any_band()
    {
        DesktopBox[] boxes = [new("a", 0, 100, 100, 80, 100), new("b", 0, 1700, 400, 80, 100), new("c", 1, 2000, 450, 120, 150)];
        var selection = new DesktopSelection();
        selection.SetLayout(boxes, key => key);

        selection.BeginBand(ctrl: false, shift: false);
        selection.Band(DesktopBand.Hits(new BandRect(1750, 420, 2050, 480), selection.Ordered));
        Assert.Equal(["b", "c"], selection.Selected.Order());

        // Live: pulled back onto the first display, the icon on the second goes back to how it was.
        selection.Band(DesktopBand.Hits(new BandRect(1750, 420, 1900, 480), selection.Ordered));
        selection.EndBand();
        Assert.Equal(["b"], selection.Selected);

        selection.BeginBand(ctrl: false, shift: true);
        selection.Band(DesktopBand.Hits(new BandRect(1950, 420, 2050, 480), selection.Ordered));
        selection.EndBand();
        Assert.Equal(["b", "c"], selection.Selected.Order());

        selection.BeginBand(ctrl: true, shift: false);
        selection.Band(DesktopBand.Hits(new BandRect(50, 50, 2050, 480), selection.Ordered));
        selection.EndBand();
        Assert.Equal(["a"], selection.Selected);
    }
}
