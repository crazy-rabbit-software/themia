using Themia.Core.Shell;
using Themia.Tests;
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

    [Fact]
    public void S14772_a_texts_formats_stand_after_paste_in_the_row_marks_before_paragraphs()
    {
        // Listed out of order: the row still reads Cut, Copy, Paste, Bold, Italic, Strikethrough, Code, Heading, the lists, Quote.
        QuickCommand[] listed =
        [
            QuickCommand.Quote, QuickCommand.Checklist, QuickCommand.Bold, QuickCommand.Paste, QuickCommand.Heading, QuickCommand.Cut,
            QuickCommand.NumberedList, QuickCommand.Code, QuickCommand.Copy, QuickCommand.BulletList, QuickCommand.Italic, QuickCommand.Strikethrough,
        ];
        var split = ContextMenuLayout.Split([.. listed.Select(Q)]);

        Assert.Equal(
            [
                QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Paste, QuickCommand.Bold, QuickCommand.Italic, QuickCommand.Strikethrough,
                QuickCommand.Code, QuickCommand.Heading, QuickCommand.BulletList, QuickCommand.NumberedList, QuickCommand.Checklist, QuickCommand.Quote,
            ],
            split.Row.Select(i => listed[i]));
        Assert.Empty(split.List);
    }

    [Fact]
    public void S14772_only_a_text_format_may_say_in_the_row_that_it_is_in_force()
    {
        QuickCommand[] formats =
        [
            QuickCommand.Bold, QuickCommand.Italic, QuickCommand.Strikethrough, QuickCommand.Code, QuickCommand.Heading,
            QuickCommand.BulletList, QuickCommand.NumberedList, QuickCommand.Checklist, QuickCommand.Quote,
        ];

        foreach (var quick in Enum.GetValues<QuickCommand>())
        {
            Assert.Equal(formats.Contains(quick), ContextMenuLayout.IsFormat(quick));
        }
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
        bool paste = false,
        bool refresh = false)
        => new(edit, front, back, count, paste, CanRefresh: refresh);

    private static List<ShellCommand> Commands(IReadOnlyList<ShellMenuItem> items) => [.. items.Select(i => i.Command)];

    [Fact]
    public void S1743_the_card_row_is_cut_copy_paste_delete()
    {
        var items = ShellMenus.ForCard(Card());
        var split = ContextMenuLayout.Split([.. items.Select(i => i.Line)]);

        // No Duplicate: Copy then Paste does that, one way to do it. No Settings: the widget's
        // options are on its page beside it ([S-6536]).
        Assert.Equal(
            [ShellCommand.Cut, ShellCommand.Copy, ShellCommand.Paste, ShellCommand.Delete],
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
    public void S1743_the_list_keeps_view_actions_layering_and_edit_mode()
    {
        var items = ShellMenus.ForCard(Card());
        var split = ContextMenuLayout.Split([.. items.Select(i => i.Line)]);
        var list = split.List.Where(i => i >= 0).Select(i => items[i].Command).ToList();

        Assert.Equal(
            [ShellCommand.WidgetActions, ShellCommand.BringToFront, ShellCommand.SendToBack, ShellCommand.ToggleEditMode, ShellCommand.ToggleLockWidgets],
            list);
    }

    // ---- [S-1758] the card menu outside edit mode -------------------------------------------------

    private static readonly ShellCommand[] Editing =
    [
        ShellCommand.Cut, ShellCommand.Copy, ShellCommand.Paste, ShellCommand.Delete,
        ShellCommand.BringToFront, ShellCommand.SendToBack, ShellCommand.AddWidget, ShellCommand.SelectAll,
    ];

    [Fact]
    public void S1758_outside_edit_mode_the_card_menu_has_no_editing_commands()
    {
        // User report 2026-10-17: Cut and Copy on a widget outside edit mode did nothing visible. [S-14321] Its Delete is the
        // desktop's (DeleteOnDesktop), not edit mode's.
        foreach (var paste in new[] { false, true })
        {
            var items = ShellMenus.ForCard(Card(edit: false, paste: paste, count: 3));
            Assert.DoesNotContain(items, i => Editing.Contains(i.Command));
        }
    }

    [Fact]
    public void S14321_outside_edit_mode_the_card_menu_is_the_widgets_lines_delete_then_the_desktop_menu()
    {
        // Owner, 2026-11-20, asked what "extended" holds: "Widget + desktop menu".
        var items = ShellMenus.ForCard(Card(edit: false));

        // [S-15832] Widget settings and [S-15831] Delete stand among the widget's own lines (the row takes them, below).
        Assert.Equal(
            [ShellCommand.WidgetActions, ShellCommand.WidgetSettings, ShellCommand.DeleteOnDesktop, ShellCommand.Separator, ShellCommand.DesktopMenu],
            Commands(items));
    }

    [Fact]
    public void S14321_with_lock_widgets_on_the_card_menu_has_no_delete()
    {
        var items = ShellMenus.ForCard(Card(edit: false) with { WidgetsLocked = true });

        Assert.Equal([ShellCommand.WidgetActions, ShellCommand.WidgetSettings, ShellCommand.Separator, ShellCommand.DesktopMenu], Commands(items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S14321_edit_mode_lock_widgets_and_add_widget_appear_once_the_desktop_menus(bool locked)
    {
        // The card's own part holds none of them; the desktop's background menu, which follows whole, holds each once.
        var card = ShellMenus.ForCard(Card(edit: false) with { WidgetsLocked = locked });
        Assert.DoesNotContain(card, i => i.Command is ShellCommand.ToggleEditMode or ShellCommand.ToggleLockWidgets or ShellCommand.AddWidget);

        var desktop = Desktop.DesktopBackground.Menu(new(ClipboardHasFiles: true, HasTerminal: true, WidgetsLocked: locked));
        foreach (var command in new[] { Desktop.DesktopMenuCommand.EditMode, Desktop.DesktopMenuCommand.LockWidgets, Desktop.DesktopMenuCommand.AddWidget })
        {
            Assert.Single(desktop, l => l.Command == command);
        }

        Assert.Single(card, i => i.Command == ShellCommand.DesktopMenu);
    }

    [Fact]
    public void S14321_a_widgets_own_refresh_says_it_is_the_widgets_beside_the_desktops()
    {
        Assert.Equal("Refresh widget", ShellMenus.WidgetLine("Refresh", "Refresh", "Refresh widget"));

        // Any other line keeps its words; a kit's line with the age already differs.
        Assert.Equal("Open C: in Explorer", ShellMenus.WidgetLine("Open C: in Explorer", "Refresh", "Refresh widget"));
        Assert.Equal("Refresh · updated 5 min ago", ShellMenus.WidgetLine("Refresh · updated 5 min ago", "Refresh", "Refresh widget"));
    }

    [Fact]
    public void S1758_in_edit_mode_the_card_menu_keeps_its_editing_commands()
    {
        var items = ShellMenus.ForCard(Card(edit: true));
        Assert.Contains(items, i => i.Command == ShellCommand.Cut);
        Assert.Contains(items, i => i.Command == ShellCommand.Delete);
        Assert.Contains(items, i => i.Command == ShellCommand.BringToFront);
    }

    // ---- [S-15830]..[S-15833] the widget's icon row -------------------------------------------------

    private static List<ShellCommand> Row(CardMenuFacts facts)
    {
        var items = ShellMenus.ForCard(facts);
        return [.. ContextMenuLayout.Split([.. items.Select(i => i.Line)]).Row.Select(i => items[i].Command)];
    }

    private static List<ShellCommand> List(CardMenuFacts facts)
    {
        var items = ShellMenus.ForCard(facts);
        return [.. ContextMenuLayout.Split([.. items.Select(i => i.Line)]).List.Where(i => i >= 0).Select(i => items[i].Command)];
    }

    [Fact]
    public void S15830_a_widget_that_refreshes_has_refresh_settings_and_delete_in_its_row()
    {
        // Owner, 2026-11-21: "add a top row for the right click menus on the widgets ... for example for the delete button or
        // the refresh button".
        var facts = Card(edit: false, refresh: true);

        Assert.Equal([ShellCommand.Refresh, ShellCommand.WidgetSettings, ShellCommand.DeleteOnDesktop], Row(facts));

        // Each once: none of them stays a line too; the view's other commands and the desktop's menu are the list.
        Assert.Equal([ShellCommand.WidgetActions, ShellCommand.DesktopMenu], List(facts));
    }

    [Fact]
    public void S15830_the_rows_commands_are_the_row_commands_windows_names_in_its_order()
    {
        var items = ShellMenus.ForCard(Card(edit: false, refresh: true));
        Assert.Equal(QuickCommand.Refresh, items.Single(i => i.Command == ShellCommand.Refresh).Quick);
        Assert.Equal(QuickCommand.Settings, items.Single(i => i.Command == ShellCommand.WidgetSettings).Quick);
        Assert.Equal(QuickCommand.Delete, items.Single(i => i.Command == ShellCommand.DeleteOnDesktop).Quick);

        // Refresh before Settings before Delete, whatever order a caller lists them in.
        var split = ContextMenuLayout.Split([Q(QuickCommand.Delete), Q(QuickCommand.Refresh), Q(QuickCommand.Settings)]);
        Assert.Equal([1, 2, 0], split.Row);
    }

    [Fact]
    public void S15831_a_widget_without_refresh_has_settings_and_delete_in_its_row()
    {
        Assert.Equal([ShellCommand.WidgetSettings, ShellCommand.DeleteOnDesktop], Row(Card(edit: false)));
        Assert.DoesNotContain(ShellCommand.Refresh, Commands(ShellMenus.ForCard(Card(edit: false))));
    }

    [Fact]
    public void S15831_with_lock_widgets_on_the_row_loses_delete_and_one_command_left_is_no_row()
    {
        // Refresh and Settings are still two: a row.
        Assert.Equal([ShellCommand.Refresh, ShellCommand.WidgetSettings], Row(Card(edit: false, refresh: true) with { WidgetsLocked = true }));

        // Settings alone is a labelled line ([S-1741]): no row, never an empty one.
        var locked = Card(edit: false) with { WidgetsLocked = true };
        Assert.Empty(Row(locked));
        Assert.Contains(ShellCommand.WidgetSettings, List(locked));
    }

    [Fact]
    public void S15833_several_selected_the_row_holds_what_acts_on_all_and_refresh_stays_a_line()
    {
        // Settings is the page of several, Delete deletes them all; the clicked widget's Refresh is its own, a line among its commands.
        var many = Card(edit: false, count: 3, refresh: true);

        Assert.Equal([ShellCommand.WidgetSettings, ShellCommand.DeleteOnDesktop], Row(many));
        Assert.DoesNotContain(ShellCommand.Refresh, Commands(ShellMenus.ForCard(many)));
        Assert.False(ShellMenus.RefreshInRow(many));
        Assert.True(ShellMenus.RefreshInRow(Card(edit: false, refresh: true)));
    }

    [Fact]
    public void S15830_in_edit_mode_the_widgets_refresh_joins_cut_copy_paste_and_delete_for_one_widget()
    {
        Assert.Equal(
            [ShellCommand.Cut, ShellCommand.Copy, ShellCommand.Paste, ShellCommand.Refresh, ShellCommand.Delete],
            Row(Card(edit: true, refresh: true)));

        // Several selected: the row is the selection's; no Settings in edit mode, its page opens beside the selection ([S-1743]).
        Assert.Equal([ShellCommand.Cut, ShellCommand.Copy, ShellCommand.Paste, ShellCommand.Delete], Row(Card(edit: true, count: 2, refresh: true)));
        Assert.DoesNotContain(ShellCommand.WidgetSettings, Commands(ShellMenus.ForCard(Card(edit: true, refresh: true))));
    }

    // ---- [S-1744] empty surface --------------------------------------------------------------------

    [Fact]
    public void S1744_empty_surface_has_a_menu_only_in_edit_mode_and_not_while_placing()
    {
        Assert.Empty(ShellMenus.ForSurface(new(EditMode: false, Placing: false, CanPaste: true, HasWidgets: true)));
        Assert.Empty(ShellMenus.ForSurface(new(EditMode: true, Placing: true, CanPaste: true, HasWidgets: true)));
        Assert.NotEmpty(ShellMenus.ForSurface(new(EditMode: true, Placing: false, CanPaste: false, HasWidgets: false)));
    }

    [Fact]
    public void S1744_empty_surface_offers_add_paste_select_all_then_the_bars_menus_and_done()
    {
        var items = ShellMenus.ForSurface(new(EditMode: true, Placing: false, CanPaste: false, HasWidgets: false));
        var split = ContextMenuLayout.Split([.. items.Select(i => i.Line)]);

        // A list alone, as Windows' desktop menu: Paste is the one icon-row command, and one is no row.
        Assert.Empty(split.Row);
        Assert.Equal(
            [
                ShellCommand.AddWidget, ShellCommand.Paste, ShellCommand.SelectAll,
                ShellCommand.Display, ShellCommand.View, ShellCommand.Settings, ShellCommand.ToggleEditMode,
            ],
            split.List.Where(i => i >= 0).Select(i => items[i].Command));
        Assert.Equal(2, items.Count(i => i.IsSeparator));

        Assert.False(items.Single(i => i.Command == ShellCommand.Paste).Enabled);
        Assert.False(items.Single(i => i.Command == ShellCommand.SelectAll).Enabled);
    }

    [Fact]
    public void S3590_the_surface_menu_is_an_action_menu_it_holds_no_setting()
    {
        var on = ShellMenus.ForSurface(new(EditMode: true, Placing: false, CanPaste: true, HasWidgets: true));

        // A setting is a switch or a tick: the surface menu has neither (Snap is the bar's alone, [S-3516]).
        Assert.All(on, i => Assert.Null(i.Checked));
        Assert.True(on.Single(i => i.Command == ShellCommand.Paste).Enabled);
        Assert.True(on.Single(i => i.Command == ShellCommand.SelectAll).Enabled);
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

    // ---- [S-12951] Lock widgets ----------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S12951_lock_widgets_stands_under_edit_mode_on_the_card_menus_checked_while_on(bool locked)
    {
        // Edit mode's card menu holds its own line; [S-14321] outside edit mode the card menu holds the desktop's whole, and its line.
        var items = ShellMenus.ForCard(Card(edit: true) with { WidgetsLocked = locked });
        var commands = Commands(items);
        var at = commands.IndexOf(ShellCommand.ToggleLockWidgets);
        Assert.Equal(ShellCommand.ToggleEditMode, commands[at - 1]);
        Assert.Equal(locked, items[at].Checked);
        Assert.Null(items[at].Quick);
        Assert.Contains(ShellCommand.DesktopMenu, Commands(ShellMenus.ForCard(Card(edit: false) with { WidgetsLocked = locked })));

        // The desktop's background menu ([S-5548]) and a desktop icon's ([S-5582]): the same line under Edit mode.
        var background = Desktop.DesktopBackground.Menu(new(ClipboardHasFiles: false, HasTerminal: false, WidgetsLocked: locked));
        var line = background.Single(l => l.Command == Desktop.DesktopMenuCommand.LockWidgets);
        Assert.Equal(locked, line.Checked);
        Assert.Equal(Desktop.DesktopMenuCommand.EditMode, background[background.ToList().IndexOf(line) - 1].Command);
        Assert.False(Desktop.DesktopBackgroundFacts.Of(Model.AppState.CreateDefault(), false, false).WidgetsLocked);
    }

    // ---- [S-12952] one leading column ------------------------------------------------------------------

    [Fact]
    public void S12952_a_toggle_shows_its_mark_in_the_icon_slot_and_nothing_while_off()
    {
        Assert.Equal(Themia.App.Shell.Glyphs.Done, MenuMarks.Slot(true, choice: false, Themia.App.Shell.Glyphs.Edit));
        Assert.Null(MenuMarks.Slot(false, choice: false, Themia.App.Shell.Glyphs.Edit));
        Assert.Equal(Themia.App.Shell.Glyphs.RadioBullet, MenuMarks.Slot(true, choice: true, null));
        Assert.Null(MenuMarks.Slot(false, choice: true, null));
        Assert.Equal(Themia.App.Shell.Glyphs.Edit, MenuMarks.Slot(null, choice: false, Themia.App.Shell.Glyphs.Edit));
    }

    /// <summary>
    /// [S-12952] Regression (owner, 2026-11-17: "there is one column too much on the left side in the context menu"):
    /// a toggle or radio flyout item makes WinUI reserve a check column beside the icons. No shipped menu builds one, the
    /// view bar's select menu included (its shown view's dot stands in its mark's slot).
    /// </summary>
    [Fact]
    public void S12952_no_menu_builds_a_toggle_or_radio_item_that_adds_a_second_leading_column()
    {
        string[] allowed = [];
        var offenders = RepoFiles.CSharp(RepoFiles.Src)
            .Where(f => File.ReadLines(f).Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                && System.Text.RegularExpressions.Regex.IsMatch(line, @"\b(ToggleMenuFlyoutItem|RadioMenuFlyoutItem|AppBarToggleButton)\b")))
            .Select(f => Path.GetRelativePath(RepoFiles.Root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(allowed, offenders);
    }
}
