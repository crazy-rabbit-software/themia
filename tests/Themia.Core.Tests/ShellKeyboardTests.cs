using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1970]..[S-1979] Keyboard in the shell: the edit-mode key map, the nudge, Tab, type-ahead and F6.</summary>
public sealed class ShellKeyboardTests
{
    // 1000 x 500 DIPs: 100 x 50 cells of 10 DIPs.
    private static readonly ScreenMetrics Screen = new(1000, 500);

    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;

    private static WidgetInstance At(string id, double x, double y, double w = 0.1, double h = 0.1)
    {
        var widget = WidgetInstance.Create(WidgetKind.Clock, new FracRect(x, y, w, h));
        widget.Id = id;
        return widget;
    }

    private static ViewState View(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("v");
        view.Widgets.AddRange(widgets);
        return view;
    }

    private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 9);

    // ---- [S-70] / [S-1970]..[S-1976] the key map -------------------------------------------------

    [Theory]
    [InlineData(0x43, false, true, false, EditCommand.Copy)]
    [InlineData(0x58, false, true, false, EditCommand.Cut)]
    [InlineData(0x56, false, true, false, EditCommand.Paste)]
    [InlineData(0x41, false, true, false, EditCommand.SelectAll)]
    [InlineData(0x5A, false, true, false, EditCommand.Undo)]
    [InlineData(0x5A, true, true, false, EditCommand.Redo)]
    [InlineData(0x59, false, true, false, EditCommand.Redo)]
    [InlineData(0x2E, false, false, false, EditCommand.Delete)]
    [InlineData(0x1B, false, false, false, EditCommand.Back)]
    [InlineData(0x0D, false, false, false, EditCommand.OpenSettings)]
    [InlineData(0x5D, false, false, false, EditCommand.OpenMenu)]
    [InlineData(0x79, true, false, false, EditCommand.OpenMenu)]
    [InlineData(0x75, false, false, false, EditCommand.SwitchPane)]
    [InlineData(0x09, false, false, false, EditCommand.Cycle)]
    public void S70_S1974_edit_mode_keys_map_to_their_command(int vk, bool shift, bool ctrl, bool alt, EditCommand expected)
        => Assert.Equal(expected, EditKeys.Decide(vk, shift, ctrl, alt).Command);

    [Theory]
    [InlineData(0x43, false, true, true)] // Ctrl+Alt+C is AltGr+C
    [InlineData(VkLeft, false, true, true)]
    [InlineData(0x43, true, true, false)] // Ctrl+Shift+C is somebody else's
    [InlineData(0x43, false, false, false)] // a plain letter
    [InlineData(0x79, false, false, false)] // F10 alone is the system's
    [InlineData(0x2E, true, false, false)] // Shift+Delete
    [InlineData(0x09, false, false, true)] // Alt+Tab
    [InlineData(0x44, false, true, false)] // Ctrl+D: no Duplicate, Copy then Paste does it
    public void S1976_other_chords_are_not_edit_keys(int vk, bool shift, bool ctrl, bool alt)
        => Assert.Equal(EditCommand.None, EditKeys.Decide(vk, shift, ctrl, alt).Command);

    [Fact]
    public void S1998_ctrl_n_opens_the_add_menu_and_nothing_else_does()
    {
        Assert.Equal(EditCommand.AddWidget, EditKeys.Decide(0x4E, false, true, false).Command);
        Assert.Equal(EditCommand.None, EditKeys.Decide(0x4E, true, true, false).Command);
        Assert.Equal(EditCommand.None, EditKeys.Decide(0x4E, false, true, true).Command); // AltGr+N
        Assert.Equal(EditCommand.None, EditKeys.Decide(0x4E, false, false, false).Command);
    }

    [Fact]
    public void S1998_the_hotkeys_are_one_table_with_distinct_ids_and_no_repeat()
    {
        var all = ShellHotkeys.All;

        Assert.Equal(Enum.GetValues<ShellHotkey>().Length, all.Count);
        Assert.Equal(all.Count, all.Select(b => b.Hotkey).Distinct().Count());
        Assert.Equal(all.Count, all.Select(b => (b.Modifiers, b.VirtualKey)).Distinct().Count());
        Assert.All(all, b => Assert.True(b.Modifiers.HasFlag(HotkeyModifiers.NoRepeat)));
        Assert.All(all, b => Assert.True((int)b.Hotkey > 0)); // RegisterHotKey ids start at 1

        // [S-65] Unchanged: Ctrl+Alt+W toggles edit mode; Shift adds the add menu.
        var toggle = all.Single(b => b.Hotkey == ShellHotkey.ToggleEditMode);
        var add = all.Single(b => b.Hotkey == ShellHotkey.AddWidget);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat, toggle.Modifiers);
        Assert.Equal(toggle.Modifiers | HotkeyModifiers.Shift, add.Modifiers);
        Assert.Equal(0x57u, add.VirtualKey);
    }

    [Fact]
    public void S1998_a_hotkey_message_maps_back_only_to_our_ids()
    {
        Assert.Equal(ShellHotkey.ToggleEditMode, ShellHotkeys.FromId(1));
        Assert.Equal(ShellHotkey.AddWidget, ShellHotkeys.FromId(2));
        Assert.Null(ShellHotkeys.FromId(0));
        Assert.Null(ShellHotkeys.FromId(3));
        Assert.Null(ShellHotkeys.FromId(-1)); // IDHOT_SNAPDESKTOP and friends are negative
    }

    [Fact]
    public void S1998_modifier_flags_are_windows_own_values()
    {
        Assert.Equal(0x0001u, (uint)HotkeyModifiers.Alt);
        Assert.Equal(0x0002u, (uint)HotkeyModifiers.Control);
        Assert.Equal(0x0004u, (uint)HotkeyModifiers.Shift);
        Assert.Equal(0x4000u, (uint)HotkeyModifiers.NoRepeat);
    }

    [Fact]
    public void S904_ctrl_digits_and_keypad_digits_name_a_view()
    {
        Assert.Equal(new EditKeyAction(EditCommand.ShowView, ViewNumber: 1), EditKeys.Decide(0x31, false, true, false));
        Assert.Equal(new EditKeyAction(EditCommand.ShowView, ViewNumber: 9), EditKeys.Decide(0x39, false, true, false));
        Assert.Equal(new EditKeyAction(EditCommand.ShowView, ViewNumber: 3), EditKeys.Decide(0x63, false, true, false));
        Assert.Equal(EditCommand.None, EditKeys.Decide(0x30, false, true, false).Command);
    }

    [Fact]
    public void S1972_modifiers_pick_move_or_resize_and_the_step()
    {
        Assert.Equal(new EditKeyAction(EditCommand.Move, NudgeDirection.Left, NudgeStep.Cell), EditKeys.Decide(VkLeft, false, false, false));
        Assert.Equal(new EditKeyAction(EditCommand.Move, NudgeDirection.Up, NudgeStep.Far), EditKeys.Decide(VkUp, false, true, false));
        Assert.Equal(new EditKeyAction(EditCommand.Move, NudgeDirection.Right, NudgeStep.Fine), EditKeys.Decide(VkRight, false, false, true));
        Assert.Equal(new EditKeyAction(EditCommand.Resize, NudgeDirection.Down, NudgeStep.Cell), EditKeys.Decide(VkDown, true, false, false));
        Assert.Equal(new EditKeyAction(EditCommand.Resize, NudgeDirection.Down, NudgeStep.Far), EditKeys.Decide(VkDown, true, true, false));
        Assert.Equal(new EditKeyAction(EditCommand.Resize, NudgeDirection.Right, NudgeStep.Fine), EditKeys.Decide(VkRight, true, false, true));
    }

    [Fact]
    public void S1973_shift_tab_cycles_backwards_and_shift_f6_switches_back()
    {
        Assert.True(EditKeys.Decide(0x09, true, false, false).Backwards);
        Assert.False(EditKeys.Decide(0x09, false, false, false).Backwards);
        Assert.True(EditKeys.Decide(0x75, true, false, false).Backwards);
    }

    // ---- [S-1970]..[S-1972] the nudge ------------------------------------------------------------

    [Fact]
    public void S1970_an_arrow_moves_one_cell_on_its_axis_only()
    {
        var moved = KeyboardNudge.Move([new FracRect(0.2, 0.2, 0.1, 0.1)], NudgeDirection.Right, NudgeStep.Cell, Screen, snapEnabled: true);

        Near(0.21, moved[0].X);
        Near(0.2, moved[0].Y);
        Near(0.1, moved[0].W);
        Near(0.1, moved[0].H);

        var down = KeyboardNudge.Move([new FracRect(0.2, 0.2, 0.1, 0.1)], NudgeDirection.Down, NudgeStep.Cell, Screen, snapEnabled: true);
        Near(0.2, down[0].X);
        Near(0.22, down[0].Y); // 10 DIPs of 500
    }

    [Fact]
    public void S1972_ctrl_moves_ten_cells_and_alt_one_dip_off_the_grid()
    {
        var far = KeyboardNudge.Move([new FracRect(0.2, 0.2, 0.1, 0.1)], NudgeDirection.Left, NudgeStep.Far, Screen, snapEnabled: true);
        Near(0.1, far[0].X);

        var fine = KeyboardNudge.Move([new FracRect(0.2, 0.2, 0.1, 0.1)], NudgeDirection.Right, NudgeStep.Fine, Screen, snapEnabled: true);
        Near(0.201, fine[0].X); // not rounded back onto the grid
    }

    [Fact]
    public void S1970_an_off_grid_widget_lands_on_the_grid_and_still_moves()
    {
        // 4 DIPs off the grid: one press right lands on the next line, never back where it was.
        var moved = KeyboardNudge.Move([new FracRect(0.204, 0.2, 0.1, 0.1)], NudgeDirection.Right, NudgeStep.Cell, Screen, snapEnabled: true);
        Near(0.21, moved[0].X);

        var left = KeyboardNudge.Move([new FracRect(0.204, 0.2, 0.1, 0.1)], NudgeDirection.Left, NudgeStep.Cell, Screen, snapEnabled: true);
        Near(0.19, left[0].X);
    }

    [Fact]
    public void S1970_S6608_a_block_moves_together_past_the_edge_and_stops_whole_at_the_visible_minimum()
    {
        FracRect[] block = [new(0.0, 0.1, 0.1, 0.1), new(0.3, 0.1, 0.1, 0.1)];

        var moved = KeyboardNudge.Move(block, NudgeDirection.Left, NudgeStep.Cell, Screen, snapEnabled: true);

        // Over the edge by one cell, both of them: the block never pulls apart.
        Near(-0.01, moved[0].X);
        Near(0.29, moved[1].X);

        var far = KeyboardNudge.Move(block, NudgeDirection.Left, NudgeStep.Far, Screen, snapEnabled: true);
        Near(0.032, far[0].Right);
        Near(0.3, far[1].X - far[0].X);

        var right = KeyboardNudge.Move(block, NudgeDirection.Right, NudgeStep.Cell, Screen, snapEnabled: true);
        Near(0.01, right[0].X);
        Near(0.31, right[1].X);
    }

    [Fact]
    public void S1970_S6608_a_move_past_the_work_area_keeps_the_visible_minimum_on_it()
    {
        var screen = Screen with { Allowed = new FracRect(0, 0, 1, 0.9) };
        var moved = KeyboardNudge.Move([new FracRect(0.2, 0.8, 0.1, 0.1)], NudgeDirection.Down, NudgeStep.Far, screen, snapEnabled: true);

        Near(0.9 - (32.0 / 500), moved[0].Y);
    }

    [Fact]
    public void S1971_shift_arrows_resize_by_the_right_and_bottom_edge()
    {
        var rect = new FracRect(0.2, 0.2, 0.1, 0.1);

        var wider = KeyboardNudge.Resize(rect, NudgeDirection.Right, NudgeStep.Cell, Screen, snapEnabled: true);
        Near(0.2, wider.X);
        Near(0.11, wider.W);
        Near(0.1, wider.H);

        var shorter = KeyboardNudge.Resize(rect, NudgeDirection.Up, NudgeStep.Cell, Screen, snapEnabled: true);
        Near(0.2, shorter.Y);
        Near(0.08, shorter.H);
        Near(0.1, shorter.W);
    }

    [Fact]
    public void S1971_a_shrink_stops_at_the_size_floor()
    {
        var small = new FracRect(0.2, 0.2, 0.04, 0.1); // 40 DIPs wide; the floor is 32

        var once = KeyboardNudge.Resize(small, NudgeDirection.Left, NudgeStep.Far, Screen, snapEnabled: false);

        Near(GridGeometry.MinWidgetPx / Screen.Width, once.W);
        Near(0.2, once.X);
    }

    [Fact]
    public void S1971_S6608_a_grow_may_pass_the_screen_edge()
    {
        var rect = new FracRect(0.85, 0.2, 0.1, 0.1);

        var grown = KeyboardNudge.Resize(rect, NudgeDirection.Right, NudgeStep.Far, Screen, snapEnabled: true);

        Near(0.85, grown.X);
        Near(1.05, grown.Right);
    }

    [Fact]
    public void S1970_an_unusable_screen_moves_nothing()
    {
        var rect = new FracRect(0.2, 0.2, 0.1, 0.1);
        Assert.Equal(rect, KeyboardNudge.Move([rect], NudgeDirection.Right, NudgeStep.Cell, new ScreenMetrics(0, 0), true)[0]);
        Assert.Equal(rect, KeyboardNudge.Resize(rect, NudgeDirection.Right, NudgeStep.Cell, new ScreenMetrics(0, 0), true));
    }

    // ---- [S-1973] Tab through the widgets --------------------------------------------------------

    [Fact]
    public void S1973_tab_walks_in_reading_order_and_wraps()
    {
        // View order is b, c, a; reading order is a (top), then b (left), then c.
        var view = View(At("b", 0.1, 0.5), At("c", 0.6, 0.5), At("a", 0.8, 0.1));

        Assert.Equal("a", SelectionRules.Cycle(view, null, backwards: false));
        Assert.Equal("b", SelectionRules.Cycle(view, "a", backwards: false));
        Assert.Equal("c", SelectionRules.Cycle(view, "b", backwards: false));
        Assert.Equal("a", SelectionRules.Cycle(view, "c", backwards: false));
        Assert.Equal("c", SelectionRules.Cycle(view, "a", backwards: true));
        Assert.Equal("c", SelectionRules.Cycle(view, null, backwards: true));
    }

    [Fact]
    public void S1973_tab_skips_hidden_widgets_and_a_missing_current_starts_over()
    {
        var view = View(At("a", 0.1, 0.1), At("b", 0.2, 0.1), At("c", 0.3, 0.1));

        Assert.Equal("c", SelectionRules.Cycle(view, "a", false, new HashSet<string> { "b" }));
        Assert.Equal("a", SelectionRules.Cycle(view, "gone", false));
        Assert.Null(SelectionRules.Cycle(View(), null, false));
    }

    // ---- [S-1978] type-ahead ---------------------------------------------------------------------

    private static readonly string[] Pages = ["General", "Appearance", "Accounts", "Displays", "Views", "License", "About"];

    [Fact]
    public void S1978_letters_typed_quickly_build_one_prefix()
    {
        var typeAhead = new TypeAhead();

        Assert.Equal(1, typeAhead.Type('A', 0, Pages, 0));
        Assert.Equal(2, typeAhead.Type('c', 300, Pages, 1));
        Assert.Equal("Ac", typeAhead.Prefix);
        Assert.Equal(4, typeAhead.Type('v', 2000, Pages, 2));
        Assert.Equal("v", typeAhead.Prefix);

        typeAhead.Reset();
        Assert.Equal(string.Empty, typeAhead.Prefix);
    }

    [Fact]
    public void S1978_a_prefix_lands_on_the_first_match_after_the_focus()
    {
        Assert.Equal(1, TypeAhead.Find(Pages, 0, "a"));
        Assert.Equal(2, TypeAhead.Find(Pages, 1, "ac"));
        Assert.Equal(1, TypeAhead.Find(Pages, 1, "ap")); // still matches: stays
        Assert.Equal(4, TypeAhead.Find(Pages, -1, "v"));
        Assert.Null(TypeAhead.Find(Pages, 0, "q"));
        Assert.Null(TypeAhead.Find(Pages, 0, string.Empty));
        Assert.Null(TypeAhead.Find([], 0, "a"));
    }

    [Fact]
    public void S1978_repeating_a_letter_walks_every_entry_with_that_initial_and_wraps()
    {
        Assert.Equal(2, TypeAhead.Find(Pages, 1, "aa"));
        Assert.Equal(6, TypeAhead.Find(Pages, 2, "aaa"));
        Assert.Equal(1, TypeAhead.Find(Pages, 6, "aaaa"));
    }

    [Fact]
    public void S1978_a_lone_space_or_a_control_character_does_not_type()
    {
        var typeAhead = new TypeAhead();

        Assert.Null(typeAhead.Type(' ', 0, Pages, 0));
        Assert.Null(typeAhead.Type('	', 10, Pages, 0));
        Assert.Null(typeAhead.Type('', 20, Pages, 0));
        Assert.Equal(string.Empty, typeAhead.Prefix);
        Assert.Equal(1, typeAhead.Type('Ä', 30, ["General", "Änderungen"], 0));
    }

    // ---- [S-1977] the settings window's keys -----------------------------------------------------

    [Fact]
    public void S1977_settings_keys()
    {
        Assert.Equal(SettingsKey.FocusSearch, SettingsKeys.Decide(0x46, false, true, false));
        Assert.Equal(SettingsKey.FocusSearch, SettingsKeys.Decide(0x45, false, true, false));
        Assert.Equal(SettingsKey.Close, SettingsKeys.Decide(0x57, false, true, false));
        Assert.Equal(SettingsKey.NextPane, SettingsKeys.Decide(0x75, false, false, false));
        Assert.Equal(SettingsKey.PreviousPane, SettingsKeys.Decide(0x75, true, false, false));
        Assert.Equal(SettingsKey.None, SettingsKeys.Decide(0x46, false, false, false));
        Assert.Equal(SettingsKey.None, SettingsKeys.Decide(0x46, false, true, true));
        Assert.Equal(SettingsKey.None, SettingsKeys.Decide(0x46, true, true, false));
    }

    [Fact]
    public void S1977_f6_cycles_the_panes_and_skips_one_that_is_away()
    {
        static bool All(SettingsPane _) => true;

        Assert.Equal(SettingsPane.Navigation, SettingsKeys.Next(SettingsPane.Search, false, All));
        Assert.Equal(SettingsPane.Page, SettingsKeys.Next(SettingsPane.Navigation, false, All));
        Assert.Equal(SettingsPane.Search, SettingsKeys.Next(SettingsPane.Page, false, All));
        Assert.Equal(SettingsPane.Page, SettingsKeys.Next(SettingsPane.Search, true, All));

        Assert.Equal(SettingsPane.Page, SettingsKeys.Next(SettingsPane.Search, false, p => p != SettingsPane.Navigation));
        Assert.Equal(SettingsPane.Search, SettingsKeys.Next(SettingsPane.Search, false, _ => false));
    }
}
