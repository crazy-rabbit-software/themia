using Themia.Core.Desktop;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-13045] The locked desktop's one selection (<see cref="DesktopSelection"/>): the icons before a fence, whatever the gesture
/// (the owner, 2026-11-19: "as soon as there is one icon selected the fence should not be selected anymore").
/// </summary>
public sealed class FenceSelectionTests
{
    private const string Desk = @"C:\Users\a\Desktop\";
    private const string Fence = "*widget:fence-1";
    private const string Other = "*widget:fence-2";
    private const string Clock = "*widget:clock";
    private const string Own = "*icon:own-1";
    private const string A = Desk + "a.txt";
    private const string B = Desk + "b.txt";
    private const string InFence = Desk + "in-fence.txt";

    /// <summary>A 90 x 110 cell grid: column c, row r.</summary>
    private static DesktopBox Box(string key, int column, int row, bool fence = false)
        => new(key, 0, column * 90, row * 110, 90, 110, fence);

    // Column 0: a, b; column 1: the fence (its item laid on its body), column 2: the clock, column 3: another fence, an own icon.
    private static DesktopSelection Desktop()
    {
        var selection = new DesktopSelection();
        selection.SetLayout(
            [
                Box(A, 0, 0),
                Box(B, 0, 1),
                Box(Fence, 1, 0, fence: true),
                Box(InFence, 1, 1),
                Box(Clock, 2, 0),
                Box(Other, 3, 0, fence: true),
                Box(Own, 3, 1),
            ],
            key => key);
        return selection;
    }

    private static void Click(DesktopSelection selection, string key, bool ctrl = false, bool shift = false)
    {
        selection.Press(key, ctrl, shift);
        selection.Release(key);
    }

    [Fact]
    public void S13045_a_fence_alone_is_selected()
    {
        var selection = Desktop();

        Click(selection, Fence);

        Assert.Equal([Fence], selection.Selected);
    }

    [Fact]
    public void S13045_ctrl_click_on_an_icon_lets_go_of_the_fence()
    {
        var selection = Desktop();
        Click(selection, Fence);

        Click(selection, A, ctrl: true);

        Assert.Equal([A], selection.Selected);
    }

    [Fact]
    public void S13045_ctrl_click_on_a_fence_never_joins_the_icons()
    {
        var selection = Desktop();
        Click(selection, A);

        Click(selection, Fence, ctrl: true);
        Click(selection, Other, ctrl: true);

        Assert.Equal([A], selection.Selected);
    }

    [Fact]
    public void S13045_other_cards_stay_with_the_icons()
    {
        var selection = Desktop();
        Click(selection, Clock);
        Click(selection, Fence, ctrl: true);

        Click(selection, Own, ctrl: true);

        Assert.Equal([Clock, Own], selection.Selected);
    }

    [Fact]
    public void S13045_a_shift_range_that_holds_an_icon_leaves_the_fences_out()
    {
        var selection = Desktop();
        Click(selection, A);

        Click(selection, Other, shift: true);

        Assert.Equal([A, B, InFence, Clock], selection.Selected);
    }

    [Fact]
    public void S13045_a_shift_range_of_cards_alone_keeps_the_fences()
    {
        var selection = Desktop();
        Click(selection, Clock);

        Click(selection, Other, shift: true);

        Assert.Equal([Clock, Other], selection.Selected);
    }

    [Fact]
    public void S13045_a_band_that_catches_any_icon_leaves_every_fence_out()
    {
        var selection = Desktop();

        selection.BeginBand(ctrl: false, shift: false);
        selection.Band([Fence, InFence, Other]);
        Assert.Equal([InFence], selection.Selected);

        // [S-13027] Not only the fence whose items it caught: one whose items it did not catch too.
        selection.Band([Fence, A, Other]);
        Assert.Equal([A], selection.Selected);
        selection.EndBand();
        Assert.Equal([A], selection.Selected);
    }

    [Fact]
    public void S13045_a_band_over_fences_and_no_icon_selects_the_fences_and_the_cards()
    {
        var selection = Desktop();

        selection.BeginBand(ctrl: false, shift: false);
        selection.Band([Fence, Clock, Other]);
        selection.EndBand();

        Assert.Equal([Fence, Clock, Other], selection.Selected);
    }

    [Fact]
    public void S13045_a_band_drawn_back_off_the_icons_gives_the_fence_back()
    {
        var selection = Desktop();

        selection.BeginBand(ctrl: false, shift: false);
        selection.Band([Fence, InFence]);
        Assert.Equal([InFence], selection.Selected);
        selection.Band([Fence]);
        selection.EndBand();

        Assert.Equal([Fence], selection.Selected);
    }

    [Fact]
    public void S13045_a_band_added_with_shift_to_a_selected_fence_lets_it_go()
    {
        var selection = Desktop();
        Click(selection, Fence);

        selection.BeginBand(ctrl: false, shift: true);
        selection.Band([B]);
        selection.EndBand();

        Assert.Equal([B], selection.Selected);
    }

    [Fact]
    public void S13045_ctrl_a_selects_everything_but_the_fences()
    {
        var selection = Desktop();

        Assert.True(selection.Key(WidgetKey.SelectAll, shift: false));

        Assert.Equal([A, B, InFence, Clock, Own], selection.Selected);
    }

    [Fact]
    public void S13045_ctrl_a_with_no_icon_shown_selects_the_fences()
    {
        var selection = new DesktopSelection();
        selection.SetLayout([Box(Fence, 0, 0, fence: true), Box(Clock, 1, 0)], key => key);

        Assert.True(selection.SelectAll());

        Assert.Equal([Fence, Clock], selection.Selected);
    }

    [Fact]
    public void S13045_an_arrow_onto_a_fence_selects_it_alone_and_shift_onto_an_icon_lets_it_go()
    {
        var selection = Desktop();
        Click(selection, A);

        Assert.True(selection.Key(WidgetKey.Right, shift: false));
        Assert.Equal([Fence], selection.Selected);

        Assert.True(selection.Key(WidgetKey.Down, shift: true));
        Assert.Equal([InFence], selection.Selected);
        Assert.Equal(InFence, selection.Focus);
    }

    [Fact]
    public void S13045_space_on_a_fence_among_icons_changes_nothing()
    {
        var selection = Desktop();
        Click(selection, A);
        Assert.True(selection.Key(WidgetKey.FocusRight, shift: false));
        Assert.Equal(Fence, selection.Focus);

        selection.Key(WidgetKey.ToggleSelection, shift: false);

        Assert.Equal([A], selection.Selected);
    }

    [Fact]
    public void S13045_a_right_press_on_a_fence_outside_the_selection_selects_it_alone()
    {
        var selection = Desktop();
        Click(selection, A);
        Click(selection, B, ctrl: true);

        selection.Context(Fence);

        Assert.Equal([Fence], selection.Selected);
    }

    [Fact]
    public void S13045_type_to_find_selects_the_icon_alone()
    {
        var selection = Desktop();
        Click(selection, Fence);

        Assert.True(selection.Type('C', nowMs: 0));

        Assert.Single(selection.Selected);
        Assert.NotEqual(Fence, selection.Selected[0]);
    }

    [Fact]
    public void S13045_a_relayout_keeps_the_rule()
    {
        var selection = new DesktopSelection();
        selection.SetLayout([Box(Fence, 0, 0, fence: true), Box(A, 1, 0)], key => key);
        selection.SelectAll();
        Assert.Equal([A], selection.Selected);

        // The icon goes from the desktop: the fence is not brought back by a relayout, nothing selected it.
        selection.SetLayout([Box(Fence, 0, 0, fence: true)], key => key);
        Assert.Empty(selection.Selected);
    }

    [Fact]
    public void S13045_item_selection_lets_go_of_what_it_is_told_and_keeps_the_focus()
    {
        var selection = new ItemSelection();
        selection.SetOrder(["a", "b", "c"]);
        selection.SelectAll();
        selection.Click(1, ctrl: true, shift: false);
        selection.Click(1, ctrl: true, shift: false);

        Assert.True(selection.Deselect(id => id == "b"));
        Assert.False(selection.Deselect(id => id == "b"));

        Assert.Equal(["a", "c"], selection.Selected);
        Assert.Equal("b", selection.Focus);
    }
}
