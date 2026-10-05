using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Context menus: the icon row, the shell's menus and click-versus-drag ([S-1740]..[S-1746]).</summary>
public sealed class ContextMenuTests
{
    private static MenuLine Q(QuickCommand quick) => MenuLine.Entry(quick);

    private static MenuLine E => MenuLine.Entry();

    private static MenuLine Sep => MenuLine.Separator;

    // ---- [S-1740] the icon row ---------------------------------------------------------------------

    [Fact]
    public void S1740_quick_commands_go_to_the_row_in_explorer_order()
    {
        // Delete listed first, Cut last: the row still reads Cut, Copy, Delete.
        var split = ContextMenuLayout.Split([Q(QuickCommand.Delete), E, Q(QuickCommand.Copy), E, Q(QuickCommand.Cut)]);

        Assert.Equal([4, 2, 0], split.Row);
        Assert.Equal([1, 3], split.List);
    }

    [Fact]
    public void S1740_a_second_entry_claiming_the_same_command_stays_in_the_list()
    {
        var split = ContextMenuLayout.Split([Q(QuickCommand.Copy), Q(QuickCommand.Delete), Q(QuickCommand.Copy)]);

        Assert.Equal([0, 1], split.Row);
        Assert.Equal([2], split.List);
    }

    [Fact]
    public void S1740_settings_sits_between_duplicate_and_delete()
    {
        var split = ContextMenuLayout.Split([Q(QuickCommand.Delete), Q(QuickCommand.Settings), Q(QuickCommand.Duplicate), Q(QuickCommand.Rename), Q(QuickCommand.Paste)]);

        Assert.Equal([4, 3, 2, 1, 0], split.Row);
        Assert.Empty(split.List);
    }

    // ---- [S-1741] no lone icon ---------------------------------------------------------------------

    [Fact]
    public void S1741_one_quick_command_alone_stays_labelled_in_the_list()
    {
        var split = ContextMenuLayout.Split([E, Q(QuickCommand.Delete)]);

        Assert.Empty(split.Row);
        Assert.Equal([0, 1], split.List);
    }

    [Fact]
    public void S1741_a_menu_with_no_quick_commands_is_all_list()
    {
        var split = ContextMenuLayout.Split([E, Sep, E]);

        Assert.Empty(split.Row);
        Assert.Equal([0, MenuSplit.SeparatorIndex, 2], split.List);
    }

    // ---- [S-1742] separators -----------------------------------------------------------------------

    [Fact]
    public void S1742_separators_collapse_leading_trailing_and_doubled()
    {
        var split = ContextMenuLayout.Split([Sep, E, Sep, Sep, E, Sep]);

        Assert.Equal([1, MenuSplit.SeparatorIndex, 4], split.List);
    }

    [Fact]
    public void S1742_a_group_emptied_by_the_row_leaves_no_double_separator()
    {
        // Cut / Copy were a group of their own between two separators; moved to the row, the list
        // must not show two dividers in a row.
        var split = ContextMenuLayout.Split([E, Sep, Q(QuickCommand.Cut), Q(QuickCommand.Copy), Sep, E]);

        Assert.Equal([2, 3], split.Row);
        Assert.Equal([0, MenuSplit.SeparatorIndex, 5], split.List);
    }

    [Fact]
    public void S1742_an_empty_menu_is_empty()
    {
        var split = ContextMenuLayout.Split([]);

        Assert.Empty(split.Row);
        Assert.Empty(split.List);
    }

    // ---- [S-1743] the card's menu ------------------------------------------------------------------

    private static CardMenuFacts Card(
        bool edit = true,
        bool front = true,
        bool back = true,
        int count = 1,
        bool paste = false)
        => new(edit, front, back, count, paste);

    private static List<ShellCommand> Commands(IReadOnlyList<ShellMenuItem> items) => [.. items.Select(i => i.Command)];

    [Fact]
    public void S1743_the_card_row_is_cut_copy_paste_duplicate_settings_delete()
    {
        var items = ShellMenus.ForCard(Card());
        var split = ContextMenuLayout.Split([.. items.Select(i => i.Line)]);

        Assert.Equal(
            [ShellCommand.Cut, ShellCommand.Copy, ShellCommand.Paste, ShellCommand.Duplicate, ShellCommand.Configure, ShellCommand.Delete],
            split.Row.Select(i => items[i].Command));
    }

    [Fact]
    public void S1743_paste_is_shown_but_disabled_with_nothing_to_paste()
    {
        Assert.False(ShellMenus.ForCard(Card(paste: false)).Single(i => i.Command == ShellCommand.Paste).Enabled);
        Assert.True(ShellMenus.ForCard(Card(paste: true)).Single(i => i.Command == ShellCommand.Paste).Enabled);
    }

    [Fact]
    public void S1743_layering_is_disabled_where_it_changes_nothing()
    {
        var front = ShellMenus.ForCard(Card(front: false, back: true)).Single(i => i.Command == ShellCommand.BringToFront);
        Assert.False(front.Enabled);

        // [S-1592] A selection always has some order to change among itself.
        var many = ShellMenus.ForCard(Card(front: false, back: false, count: 3));
        Assert.True(many.Single(i => i.Command == ShellCommand.BringToFront).Enabled);
        Assert.True(many.Single(i => i.Command == ShellCommand.SendToBack).Enabled);
    }

    [Fact]
    public void S1743_the_list_keeps_view_actions_move_and_edit_mode()
    {
        var items = ShellMenus.ForCard(Card());
        var split = ContextMenuLayout.Split([.. items.Select(i => i.Line)]);
        var list = split.List.Where(i => i >= 0).Select(i => items[i].Command).ToList();

        Assert.Equal(
            [ShellCommand.WidgetActions, ShellCommand.BringToFront, ShellCommand.SendToBack, ShellCommand.Move, ShellCommand.ToggleEditMode],
            list);
    }

    // ---- [S-1744] empty surface --------------------------------------------------------------------

    [Fact]
    public void S1744_empty_surface_has_a_menu_only_in_edit_mode_and_not_while_placing()
    {
        Assert.Empty(ShellMenus.ForSurface(new(EditMode: false, Placing: false, CanPaste: true, HasWidgets: true, GridSnap: true)));
        Assert.Empty(ShellMenus.ForSurface(new(EditMode: true, Placing: true, CanPaste: true, HasWidgets: true, GridSnap: true)));
        Assert.NotEmpty(ShellMenus.ForSurface(new(EditMode: true, Placing: false, CanPaste: false, HasWidgets: false, GridSnap: false)));
    }

    [Fact]
    public void S1744_empty_surface_offers_add_paste_select_all_snap_done_and_settings()
    {
        var items = ShellMenus.ForSurface(new(EditMode: true, Placing: false, CanPaste: false, HasWidgets: false, GridSnap: true));
        var split = ContextMenuLayout.Split([.. items.Select(i => i.Line)]);

        Assert.Equal([ShellCommand.Paste, ShellCommand.Settings], split.Row.Select(i => items[i].Command));
        Assert.Equal(
            [ShellCommand.AddWidget, ShellCommand.SelectAll, ShellCommand.SnapToGrid, ShellCommand.ToggleEditMode],
            split.List.Where(i => i >= 0).Select(i => items[i].Command));

        Assert.False(items.Single(i => i.Command == ShellCommand.Paste).Enabled);
        Assert.False(items.Single(i => i.Command == ShellCommand.SelectAll).Enabled);
        Assert.True(items.Single(i => i.Command == ShellCommand.SnapToGrid).Checked);
    }

    [Fact]
    public void S1744_snap_to_grid_shows_its_state()
    {
        var off = ShellMenus.ForSurface(new(EditMode: true, Placing: false, CanPaste: true, HasWidgets: true, GridSnap: false));

        Assert.False(off.Single(i => i.Command == ShellCommand.SnapToGrid).Checked);
        Assert.True(off.Single(i => i.Command == ShellCommand.Paste).Enabled);
        Assert.True(off.Single(i => i.Command == ShellCommand.SelectAll).Enabled);
    }

    // ---- [S-1745] click versus drag versus select ---------------------------------------------------

    [Fact]
    public void S1745_a_press_is_classified_by_mode_button_ctrl_and_handle()
    {
        Assert.Equal(CardPress.None, CardGestures.Press(editMode: false, leftButton: true, ctrl: false, onHandle: false));
        Assert.Equal(CardPress.None, CardGestures.Press(editMode: true, leftButton: false, ctrl: false, onHandle: false));
        Assert.Equal(CardPress.ToggleSelect, CardGestures.Press(editMode: true, leftButton: true, ctrl: true, onHandle: true));
        Assert.Equal(CardPress.Resize, CardGestures.Press(editMode: true, leftButton: true, ctrl: false, onHandle: true));
        Assert.Equal(CardPress.Move, CardGestures.Press(editMode: true, leftButton: true, ctrl: false, onHandle: false));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(2.9, -2.9, false)]
    [InlineData(3, 0, true)]
    [InlineData(0, -3, true)]
    [InlineData(double.NaN, 0, true)]
    public void S1745_travel_past_three_dips_on_either_axis_is_a_drag(double dx, double dy, bool travelled)
        => Assert.Equal(travelled, CardGestures.Travelled(dx, dy));

    [Fact]
    public void S1745_only_a_plain_released_click_follows_in_an_open_settings_window()
    {
        Assert.True(CardGestures.FollowsInSettings(CardPress.Move, travelled: false, released: true, settingsOpen: true));

        // A closed window is never opened by a click.
        Assert.False(CardGestures.FollowsInSettings(CardPress.Move, travelled: false, released: true, settingsOpen: false));

        // A drag, a resize, Ctrl+click, or a gesture lost to capture is not a click.
        Assert.False(CardGestures.FollowsInSettings(CardPress.Move, travelled: true, released: true, settingsOpen: true));
        Assert.False(CardGestures.FollowsInSettings(CardPress.Resize, travelled: false, released: true, settingsOpen: true));
        Assert.False(CardGestures.FollowsInSettings(CardPress.ToggleSelect, travelled: false, released: true, settingsOpen: true));
        Assert.False(CardGestures.FollowsInSettings(CardPress.Move, travelled: false, released: false, settingsOpen: true));
        Assert.False(CardGestures.FollowsInSettings(CardPress.None, travelled: false, released: true, settingsOpen: true));
    }
}
