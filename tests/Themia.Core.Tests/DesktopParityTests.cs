using Themia.Core.Desktop;
using Themia.Core.Files;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5540]..[S-5547] [S-5591] Explorer parity on the desktop: the one selection, the keys, rename, landings and moves.</summary>
public sealed class DesktopParityTests
{
    private static void Near(FracRect expected, FracRect? actual)
    {
        Assert.NotNull(actual);
        var got = actual.Value;
        Assert.True(Math.Abs(expected.X - got.X) < 1e-9 && Math.Abs(expected.Y - got.Y) < 1e-9 && Math.Abs(expected.W - got.W) < 1e-9 && Math.Abs(expected.H - got.H) < 1e-9, $"expected {expected}, got {got}");
    }

    private const string Desk = @"C:\Users\a\Desktop\";

    /// <summary>A 90 x 110 cell grid: column c, row r on display d (each display 2000 px wide).</summary>
    private static DesktopBox Box(string name, int column, int row, int display = 0)
        => new(Desk + name, display, (display * 2000) + (column * 90), row * 110, 90, 110);

    private static DesktopSelection Laid(params DesktopBox[] boxes)
    {
        var selection = new DesktopSelection();
        selection.SetLayout(boxes, key => DesktopRename.FileName(key));
        return selection;
    }

    [Fact]
    public void S5540_the_order_runs_down_each_column_then_the_next_display_by_display()
    {
        var ordered = DesktopNav.Order([Box("c", 1, 0), Box("z", 0, 0, display: 1), Box("b", 0, 1), Box("a", 0, 0), Box("a", 3, 3, display: 1)]);

        Assert.Equal([Desk + "a", Desk + "b", Desk + "c", Desk + "z"], ordered.Select(b => b.Key));
    }

    [Fact]
    public void S5540_one_selection_spans_every_display_and_survives_a_relayout()
    {
        var selection = Laid(Box("a", 0, 0), Box("b", 0, 1), Box("z", 0, 0, display: 1));

        selection.Press(Desk + "a", ctrl: false, shift: false);
        selection.Press(Desk + "z", ctrl: true, shift: false);
        Assert.Equal([Desk + "a", Desk + "z"], selection.Selected);

        // The item on the second display went (its view switched): it leaves the selection, the rest stays.
        selection.SetLayout([Box("a", 0, 0), Box("b", 0, 1)], key => key);
        Assert.Equal([Desk + "a"], selection.Selected);
    }

    [Fact]
    public void S5540_shift_click_selects_the_range_in_the_desktops_order()
    {
        var selection = Laid(Box("a", 0, 0), Box("b", 0, 1), Box("c", 1, 0), Box("d", 1, 1));

        selection.Press(Desk + "b", ctrl: false, shift: false);
        selection.Press(Desk + "c", ctrl: false, shift: true);

        Assert.Equal([Desk + "b", Desk + "c"], selection.Selected);
    }

    [Fact]
    public void S5541_arrows_move_by_place_and_stay_at_a_columns_end()
    {
        var selection = Laid(Box("a", 0, 0), Box("b", 0, 1), Box("c", 1, 0));
        selection.Press(Desk + "a", false, false);

        Assert.True(selection.Key(WidgetKey.Down, shift: false));
        Assert.Equal(Desk + "b", selection.Focus);

        // Down from the column's last item: nothing below in the column or within 45 degrees, it stays.
        Assert.True(selection.Key(WidgetKey.Down, shift: false));
        Assert.Equal(Desk + "b", selection.Focus);

        // Right from b (column 0, row 1): c is up and right, more than 45 degrees off? No: one cell right, one up is 45.
        selection.Key(WidgetKey.Up, shift: false);
        Assert.True(selection.Key(WidgetKey.Right, shift: false));
        Assert.Equal(Desk + "c", selection.Focus);
        Assert.Equal([Desk + "c"], selection.Selected);
        Assert.True(selection.ShowsFocus(Desk + "c"));
    }

    [Fact]
    public void S5541_right_from_the_last_column_reaches_the_next_display()
    {
        var selection = Laid(Box("a", 0, 0), Box("z", 0, 0, display: 1));
        selection.Press(Desk + "a", false, false);

        selection.Key(WidgetKey.Right, shift: false);

        Assert.Equal(Desk + "z", selection.Focus);
    }

    [Fact]
    public void S5541_shift_extends_ctrl_moves_the_focus_only_home_and_end_go_to_the_ends()
    {
        var selection = Laid(Box("a", 0, 0), Box("b", 0, 1), Box("c", 0, 2));
        selection.Press(Desk + "a", false, false);

        selection.Key(WidgetKey.Down, shift: true);
        Assert.Equal([Desk + "a", Desk + "b"], selection.Selected);

        selection.Key(WidgetKey.FocusDown, shift: false);
        Assert.Equal(Desk + "c", selection.Focus);
        Assert.Equal([Desk + "a", Desk + "b"], selection.Selected);

        selection.Key(WidgetKey.Home, shift: false);
        Assert.Equal([Desk + "a"], selection.Selected);
        selection.Key(WidgetKey.End, shift: false);
        Assert.Equal([Desk + "c"], selection.Selected);
    }

    [Fact]
    public void S5541_the_first_arrow_with_nothing_focused_lands_on_the_first_item()
    {
        var selection = Laid(Box("b", 0, 1), Box("a", 0, 0));

        Assert.True(selection.Key(WidgetKey.Down, shift: false));
        Assert.Equal(Desk + "a", selection.Focus);
        Assert.False(Laid().Key(WidgetKey.Down, shift: false));
    }

    [Fact]
    public void S5542_typing_selects_the_next_item_whose_name_starts_so()
    {
        var selection = Laid(Box("apple.txt", 0, 0), Box("banana.txt", 0, 1), Box("blue.txt", 0, 2));

        Assert.True(selection.Type('b', 0));
        Assert.Equal([Desk + "banana.txt"], selection.Selected);
        Assert.True(selection.Type('l', 100));
        Assert.Equal([Desk + "blue.txt"], selection.Selected);
        Assert.False(selection.Type('q', 5000));
    }

    [Fact]
    public void S5544_the_band_replaces_adds_or_toggles_and_is_live()
    {
        var selection = Laid(Box("a", 0, 0), Box("b", 0, 1), Box("c", 0, 2));
        selection.Press(Desk + "a", false, false);

        selection.BeginBand(ctrl: false, shift: false);
        selection.Band([Desk + "b", Desk + "c"]);
        Assert.Equal([Desk + "b", Desk + "c"], selection.Selected);
        selection.Band([Desk + "c"]);
        Assert.Equal([Desk + "c"], selection.Selected);
        selection.EndBand();

        selection.BeginBand(ctrl: true, shift: false);
        selection.Band([Desk + "a", Desk + "c"]);
        Assert.Equal([Desk + "a"], selection.Selected);
        selection.EndBand();

        Assert.True(selection.Clear());
        Assert.Empty(selection.Selected);
    }

    [Fact]
    public void S5543_a_renamed_item_is_selected_once_shown_under_its_new_key()
    {
        var selection = Laid(Box("old.txt", 0, 0));
        selection.SelectWhenShown(Desk + "new.txt");
        Assert.Empty(selection.Selected);

        selection.SetLayout([Box("new.txt", 0, 0)], key => key);

        Assert.Equal([Desk + "new.txt"], selection.Selected);
    }

    private static DesktopItem File(string fileName, string shown, DesktopItemKind kind = DesktopItemKind.File)
        => new(Desk + fileName, shown, kind);

    [Fact]
    public void S5543_the_box_selects_the_name_before_a_shown_extension()
    {
        Assert.Equal(("Report.docx", 6), DesktopRename.Edit(File("Report.docx", "Report.docx")));
        Assert.Equal(("Report", 6), DesktopRename.Edit(File("Report.docx", "Report")));
        Assert.Equal(("v1.2 notes", 10), DesktopRename.Edit(File("v1.2 notes", "v1.2 notes", DesktopItemKind.Folder)));
        Assert.Equal(("Edge", 4), DesktopRename.Edit(File("Edge.lnk", "Edge", DesktopItemKind.Shortcut)));
    }

    [Fact]
    public void S5543_a_hidden_extension_and_a_shortcuts_lnk_are_put_back()
    {
        Assert.Equal("Plan.docx", DesktopRename.Commit(File("Report.docx", "Report"), "Plan", []).FullName);
        Assert.Equal("Browser.lnk", DesktopRename.Commit(File("Edge.lnk", "Edge", DesktopItemKind.Shortcut), "Browser", []).FullName);
        Assert.Equal("Plan.txt", DesktopRename.Commit(File("Report.docx", "Report.docx"), "Plan.txt", []).FullName);
    }

    [Fact]
    public void S5543_unchanged_invalid_and_clashing_names()
    {
        var item = File("Report.docx", "Report.docx");

        Assert.Equal(DesktopRenameVerdict.Unchanged, DesktopRename.Commit(item, " Report.docx ", []).Verdict);

        var invalid = DesktopRename.Commit(item, "a:b.docx", []);
        Assert.Equal(DesktopRenameVerdict.Invalid, invalid.Verdict);
        Assert.Equal(FileNameProblem.InvalidCharacter, invalid.Problem);
        Assert.Equal(FileNameProblem.Empty, DesktopRename.Commit(item, "   ", []).Problem);

        Assert.Equal(DesktopRenameVerdict.Clash, DesktopRename.Commit(item, "notes.TXT", ["Notes.txt"]).Verdict);

        // A change of case alone is the same file: no clash.
        Assert.Equal(DesktopRenameVerdict.Rename, DesktopRename.Commit(item, "report.docx", ["Report.docx"]).Verdict);
    }

    [Fact]
    public void S5543_shell_items_are_not_renamed_in_place_and_siblings_are_the_same_folders()
    {
        Assert.False(DesktopRename.CanRename(new DesktopItem("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem)));
        Assert.True(DesktopRename.CanRename(File("a.txt", "a.txt")));

        DesktopItem[] listing =
        [
            File("a.txt", "a.txt"),
            File("b.txt", "b.txt"),
            new(@"C:\Users\Public\Desktop\c.txt", "c.txt", DesktopItemKind.File),
            new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem),
        ];

        Assert.Equal(["b.txt"], DesktopRename.Siblings(Desk + "a.txt", listing));
    }

    private static readonly FracRect Area = new(0, 0, 1, 1);
    private static readonly FracRect Cell = new(0, 0, 0.1, 0.25);

    [Fact]
    public void S5591_a_drop_lands_in_the_cell_under_the_point_or_the_next_free_after_it()
    {
        Near(new FracRect(0.3, 0.5, 0.1, 0.25), DesktopCells.FreeFrom([], 0.35, 0.6, Cell, Area));

        // Taken: the next one down the same column.
        Near(new FracRect(0.3, 0.75, 0.1, 0.25), DesktopCells.FreeFrom([new FracRect(0.3, 0.5, 0.1, 0.25)], 0.35, 0.6, Cell, Area));

        // A point past the last whole cell is kept inside the area.
        Near(new FracRect(0.9, 0.75, 0.1, 0.25), DesktopCells.FreeFrom([], 0.999, 0.999, Cell, Area));
    }

    [Fact]
    public void S5591_a_hint_lands_the_names_it_expects_and_is_used_up()
    {
        var landings = new DesktopLandings();
        landings.Expect(new DesktopLandingHint("v2", 0.5, 0.5, ["photo.jpg", "notes.txt"]));

        var first = landings.Place([new DesktopItem(Desk + "Photo.JPG", "Photo", DesktopItemKind.File), new DesktopItem(Desk + "download.zip", "download", DesktopItemKind.File)]);
        Assert.Equal(new DesktopLandingSpot("v2", 0.5, 0.5), first[0]);
        Assert.Null(first[1]);
        Assert.NotNull(landings.Pending);

        landings.Place([new DesktopItem(Desk + "notes.txt", "notes", DesktopItemKind.File)]);
        Assert.Null(landings.Pending);
    }

    [Fact]
    public void S5591_a_hint_without_names_takes_the_next_reconciles_arrivals_only()
    {
        var landings = new DesktopLandings();
        landings.Expect(new DesktopLandingHint("v1", 0.1, 0.1, []));

        Assert.Empty(landings.Place([]));
        Assert.NotNull(landings.Pending);

        var spots = landings.Place([new DesktopItem(Desk + "New folder", "New folder", DesktopItemKind.Folder)]);
        Assert.Equal("v1", spots[0]?.ViewId);
        Assert.Null(landings.Pending);
    }

    [Fact]
    public void S5546_a_dragged_group_keeps_its_shape_and_the_held_item_lands_under_the_pointer()
    {
        FracRect[] moving = [new(0, 0, 0.1, 0.25), new(0, 0.25, 0.1, 0.25)];

        var moved = DesktopCells.Move(moving, held: 1, 0.55, 0.6, [], Cell, Area);

        Near(new FracRect(0.5, 0.25, 0.1, 0.25), moved[0]);
        Near(new FracRect(0.5, 0.5, 0.1, 0.25), moved[1]);
    }

    [Fact]
    public void S5546_a_moved_item_that_would_cover_a_staying_widget_takes_the_next_free_cell()
    {
        var moved = DesktopCells.Move([new FracRect(0, 0, 0.1, 0.25)], held: 0, 0.55, 0.6, [new FracRect(0.5, 0.5, 0.1, 0.25)], Cell, Area);

        Near(new FracRect(0.5, 0.75, 0.1, 0.25), moved[0]);
    }

    [Fact]
    public void S5547_hidden_items_follow_explorers_two_settings()
    {
        Assert.Equal(0x60u, DesktopViewSettings.EnumFlags(showHidden: false, showSuperHidden: true));
        Assert.Equal(0xE0u, DesktopViewSettings.EnumFlags(showHidden: true, showSuperHidden: false));
        Assert.Equal(0x100E0u, DesktopViewSettings.EnumFlags(showHidden: true, showSuperHidden: true));
    }

    [Theory]
    [InlineData(0x2E, true, WidgetKey.PermanentDelete)]
    [InlineData(0x2E, false, WidgetKey.Delete)]
    [InlineData(0x74, false, WidgetKey.Refresh)]
    public void S5545_shift_delete_and_f5_are_keys(int virtualKey, bool shift, WidgetKey expected)
        => Assert.Equal(expected, WidgetKeys.FromVirtualKey(virtualKey, shift, ctrl: false, alt: false));
}
