using Themia.Core.Desktop;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5540] [S-5541] [S-5544] The locked desktop's one selection (<see cref="DesktopSelection"/>) over an item two displays
/// show (the owner, 2026-11-20: "there are two quartus desktop icons and i cannot select them together"): the user's Quartus
/// on Home and on Today, the Public desktop's beside it on Today.
/// </summary>
public sealed class DeskTwinSelectionTests
{
    private const string Desk = @"C:\Users\a\Desktop\";
    private const string Public = @"C:\Users\Public\Desktop\";

    // The owner's desktop: Home on the left display (0) shows the user's Quartus among others; Today on the right (1)
    // shows it and the Public desktop's Quartus side by side at the top-left.
    private const string User = Desk + "Quartus (Quartus Prime 24.1std).lnk";
    private const string Shared = Public + "Quartus (Quartus Prime 24.1std).lnk";

    /// <summary>A 90 x 110 cell grid: column c, row r on display d (each display 2000 px wide).</summary>
    private static DesktopBox Box(string key, int column, int row, int display = 0)
        => new(key, display, (display * 2000) + (column * 90), row * 110, 90, 110);

    private static DesktopBox[] TwoViews() =>
    [
        Box(Desk + "home1.txt", 0, 0, display: 0),
        Box(User, 3, 2, display: 0),
        Box(Desk + "home2.txt", 4, 0, display: 0),
        Box(User, 0, 0, display: 1),
        Box(Shared, 1, 0, display: 1),
        Box(Desk + "today.txt", 2, 0, display: 1),
    ];

    private static DesktopSelection Laid(DesktopBox[] boxes, int? display = null)
    {
        var selection = new DesktopSelection();
        selection.SetLayout(boxes, key => key, display);
        return selection;
    }

    [Fact]
    public void S5544_a_band_on_one_display_catches_an_item_another_display_shows_too()
    {
        var selection = Laid(TwoViews(), display: 1);

        // The band round Today's top-left: both Quartus icons as Today shows them.
        selection.BeginBand(ctrl: false, shift: false);
        selection.Band(DesktopBand.Hits(new BandRect(2010, 10, 2150, 100), selection.Boxes));
        selection.EndBand();

        Assert.Equal([User, Shared], selection.Selected);
    }

    [Fact]
    public void S5544_every_display_keeps_its_box_of_an_item_both_show()
    {
        var selection = Laid(TwoViews());

        Assert.Equal(2, selection.Boxes.Count(b => DesktopItemKeys.Comparer.Equals(b.Key, User)));
        Assert.Single(selection.Ordered, b => DesktopItemKeys.Comparer.Equals(b.Key, User));
    }

    [Fact]
    public void S5540_shift_click_runs_the_range_on_the_display_it_is_made_on()
    {
        var selection = Laid(TwoViews(), display: 1);

        selection.Press(User, ctrl: false, shift: false);
        selection.Press(Shared, ctrl: false, shift: true);

        Assert.Equal([User, Shared], selection.Selected);
    }

    [Fact]
    public void S5540_ctrl_click_holds_both()
    {
        var selection = Laid(TwoViews(), display: 1);

        selection.Press(Shared, ctrl: false, shift: false);
        selection.Press(User, ctrl: true, shift: false);

        Assert.Equal([User, Shared], selection.Selected);
    }

    [Fact]
    public void S5541_the_arrows_step_from_where_the_display_of_the_keys_shows_the_item()
    {
        var selection = Laid(TwoViews(), display: 1);
        selection.Press(User, ctrl: false, shift: false);

        Assert.True(selection.Key(WidgetKey.Right, shift: false));
        Assert.Equal(Shared, selection.Focus);
    }

    [Fact]
    public void S5540_an_item_two_displays_show_takes_its_place_on_the_display_of_the_gesture()
    {
        var boxes = TwoViews();

        Assert.Equal(0, DesktopNav.Order(boxes).Single(b => b.Key == User).Display);
        Assert.Equal(1, DesktopNav.Order(boxes, display: 1).Single(b => b.Key == User).Display);
        Assert.Equal(0, DesktopNav.Order(boxes, display: 0).Single(b => b.Key == User).Display);

        // The order stays the desktop's: display by display, down each column.
        Assert.Equal([Desk + "home1.txt", Desk + "home2.txt", User, Shared, Desk + "today.txt"], DesktopNav.Order(boxes, display: 1).Select(b => b.Key));
    }

    [Fact]
    public void S5540_a_new_display_of_the_gesture_keeps_the_selection()
    {
        var selection = Laid(TwoViews(), display: 0);
        selection.Press(User, ctrl: false, shift: false);
        selection.Press(Desk + "home2.txt", ctrl: true, shift: false);

        selection.SetLayout(TwoViews(), key => key, display: 1);

        Assert.Equal([Desk + "home2.txt", User], selection.Selected);
    }
}
