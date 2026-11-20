using System.Text.Json;
using Themia.Core.Desktop;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5593]..[S-5596] [S-5610] [S-2343] The desktop's view settings (its icon size and cell, Auto
/// arrange, Align to grid, the remembered sort), a desktop icon's tip, and the selection's page held
/// back while a drag runs.
/// </summary>
public sealed class DesktopViewTests
{
    // ---- [S-5593] the icon size and its cell -------------------------------------------------------

    [Theory]
    [InlineData(DesktopIconSize.Large, 96, 124, 148)]
    [InlineData(DesktopIconSize.Medium, 48, 76, 100)]
    [InlineData(DesktopIconSize.Small, 32, 60, 84)]
    public void S5593_each_size_has_explorers_icon_and_a_cell_that_grows_with_it(DesktopIconSize size, double icon, double width, double height)
    {
        Assert.Equal(icon, DesktopCell.Icon(size));
        Assert.Equal(new SizeDip(width, height), DesktopCell.Size(size, 2));
        Assert.Equal(new SizeDip(width, height), DesktopCell.Grid(size));

        // One name line less is one 16-DIP line less, at every size; the room round the icon never changes.
        Assert.Equal(new SizeDip(width, height - 16), DesktopCell.Size(size, 1));
        Assert.Equal(icon + 3 + 2 + 32 + 1, DesktopCell.HighlightHeight(size, 32));
        Assert.Equal(width - 4, DesktopCell.NameWidth(size, 0));
        Assert.Equal(new DesktopCellFit(icon, 2), DesktopCell.Fit(size, 5));
    }

    [Theory]
    [InlineData(DesktopIconSize.Large, 1.0, 96)]
    [InlineData(DesktopIconSize.Large, 1.5, 144)]
    [InlineData(DesktopIconSize.Large, 2.0, 192)]
    [InlineData(DesktopIconSize.Small, 1.0, 32)]
    [InlineData(DesktopIconSize.Small, 1.5, 48)]
    [InlineData(DesktopIconSize.Medium, 1.5, 72)]
    public void S5593_the_icon_is_asked_of_the_shell_at_the_displays_own_pixels(DesktopIconSize size, double scale, int pixels)
        => Assert.Equal(pixels, DesktopIconPixels.For(DesktopCell.Icon(size), scale));

    [Fact]
    public void S5593_the_wheel_steps_one_size_and_stops_at_the_ends()
    {
        Assert.Equal(DesktopIconSize.Large, DesktopCell.Step(DesktopIconSize.Medium, 120));
        Assert.Equal(DesktopIconSize.Small, DesktopCell.Step(DesktopIconSize.Medium, -120));
        Assert.Equal(DesktopIconSize.Medium, DesktopCell.Step(DesktopIconSize.Small, 1));
        Assert.Equal(DesktopIconSize.Large, DesktopCell.Step(DesktopIconSize.Large, 360));
        Assert.Equal(DesktopIconSize.Small, DesktopCell.Step(DesktopIconSize.Small, -1));
        Assert.Equal(DesktopIconSize.Medium, DesktopCell.Step(DesktopIconSize.Medium, 0));
    }

    [Fact]
    public void S5593_the_size_in_force_is_medium_until_applied_and_an_unknown_one_is_medium()
    {
        // The one static of the rule; nothing else in this assembly reads it, and it is put back.
        var before = DesktopCell.Current;
        try
        {
            DesktopCell.Apply(DesktopIconSize.Medium);
            Assert.True(DesktopCell.Apply(DesktopIconSize.Large));
            Assert.Equal(DesktopIconSize.Large, DesktopCell.Current);
            Assert.False(DesktopCell.Apply(DesktopIconSize.Large));
            Assert.True(DesktopCell.Apply((DesktopIconSize)42));
            Assert.Equal(DesktopIconSize.Medium, DesktopCell.Current);
        }
        finally
        {
            DesktopCell.Apply(before);
        }
    }

    // ---- the settings as stored --------------------------------------------------------------------

    [Fact]
    public void S5593_a_new_state_has_medium_icons_sorted_by_name()
    {
        var preferences = new AppPreferences();

        Assert.Equal(DesktopIconSize.Medium, preferences.DesktopIconSize);
        Assert.Equal(DesktopSortBy.Name, new AppState().Desktop.Sort);
    }

    [Fact]
    public void S5593_the_icon_size_is_copied_and_written_by_name()
    {
        var preferences = new AppPreferences { DesktopIconSize = DesktopIconSize.Large };

        Assert.Equal(DesktopIconSize.Large, preferences.Clone().DesktopIconSize);

        var json = JsonSerializer.Serialize(preferences, AppStateJsonContext.Default.AppPreferences).Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"desktopIconSize\":\"Large\"", json, StringComparison.Ordinal);
        Assert.Equal(DesktopIconSize.Large, JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppPreferences)!.DesktopIconSize);
    }

    [Fact]
    public void S5593_S5596_a_number_that_names_no_size_or_sort_is_repaired_to_the_default()
    {
        var state = AppState.CreateDefault();
        state.Preferences.DesktopIconSize = (DesktopIconSize)42;
        state.Desktop.Sort = (DesktopSortBy)42;

        var repairs = StateRepair.Repair(state);

        Assert.Equal(DesktopIconSize.Medium, state.Preferences.DesktopIconSize);
        Assert.Equal(DesktopSortBy.Name, state.Desktop.Sort);
        Assert.Contains(repairs, r => r.Contains("desktop icon size", StringComparison.Ordinal));
        Assert.Contains(repairs, r => r.Contains("desktop sort", StringComparison.Ordinal));
    }

    // ---- [S-2343] the selection's page and a drag ---------------------------------------------------

    [Fact]
    public void S2343_a_new_selection_opens_its_page_once()
    {
        var (shown, opens) = SelectionPage.Flyout(null, "a|", placing: false, held: false);
        Assert.True(opens);
        Assert.Equal("a|", shown);

        // The same selection again (a state change, the page closed by the user): nothing opens.
        Assert.False(SelectionPage.Flyout(shown, "a|", placing: false, held: false).Opens);
        Assert.True(SelectionPage.Flyout(shown, "a,b|", placing: false, held: false).Opens);
    }

    [Fact]
    public void S2343_a_selection_made_by_a_drag_opens_its_page_when_the_drag_ends_never_over_it()
    {
        // Regression (seen live 2026-11-14): a widget selected by a drag had its page opened at once, over
        // the widget still being dragged.
        var (shown, opens) = SelectionPage.Flyout(null, "clock|", placing: false, held: true);
        Assert.False(opens);

        // Changed 2026-11-14 (owner): the selection stays new while held, so the drag's end opens its page.
        Assert.Null(shown);
        Assert.True(SelectionPage.Flyout(shown, "clock|", placing: false, held: false).Opens);
    }

    [Fact]
    public void S2343_a_held_selection_keeps_what_was_shown_before_it()
    {
        var (shown, _) = SelectionPage.Flyout(null, "a|", placing: false, held: false);

        // Another widget pressed and dragged: nothing opens and "a" stays the one last shown.
        var (still, opens) = SelectionPage.Flyout(shown, "b|", placing: false, held: true);
        Assert.False(opens);
        Assert.Equal("a|", still);
    }

    [Theory]
    [InlineData(true, false, false, true)]   // a click on a widget, or a drag ended: its page opens
    [InlineData(true, true, false, false)]   // already open: stays as it is
    [InlineData(false, false, false, false)] // nothing selected that has a page
    [InlineData(true, false, true, false)]   // a ghost being placed
    public void S2343_letting_go_of_a_widget_opens_its_page_even_one_the_user_closed(bool shows, bool open, bool placing, bool opens)
        => Assert.Equal(opens, SelectionPage.OpensWhenReleased(shows, open, placing));

    [Fact]
    public void S2343_nothing_opens_while_a_ghost_is_being_placed()
        => Assert.False(SelectionPage.Flyout(null, "a|", placing: true, held: false).Opens);

    // ---- [S-5610] the tip ---------------------------------------------------------------------------

    private static readonly DateTime Written = new(2026, 11, 14, 10, 42, 0);

    private static DesktopItem Item(string name, DesktopItemKind kind) => new(@"C:\Users\a\Desktop\" + name, name, kind);

    [Fact]
    public void S5610_a_file_says_its_type_its_size_and_when_it_was_modified()
    {
        var tip = DesktopTip.Item(Locale.En, Item("Budget.xlsx", DesktopItemKind.File), new DesktopItemFacts("Microsoft Excel Worksheet", 2048, Written, Written), nameCut: false);

        var lines = tip!.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("Type: Microsoft Excel Worksheet", lines[0]);
        Assert.StartsWith("Size: ", lines[1], StringComparison.Ordinal);
        Assert.Contains("2", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("Date modified: ", lines[2], StringComparison.Ordinal);
        Assert.Contains("2026", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void S5610_a_folder_says_when_it_was_made_and_nothing_of_a_size()
    {
        var tip = DesktopTip.Item(Locale.En, Item("Projects", DesktopItemKind.Folder), new DesktopItemFacts(null, null, Written, Written), nameCut: false);

        Assert.StartsWith("Date created: ", tip, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', tip!);
    }

    [Fact]
    public void S5610_a_shortcut_says_where_it_points_as_explorer_writes_it()
    {
        var facts = new DesktopItemFacts("Shortcut", 1204, Written, Written, @"C:\Program Files\Planner\Planner.exe");

        Assert.Equal(@"Location: Planner.exe (C:\Program Files\Planner)", DesktopTip.Item(Locale.En, Item("Planner.lnk", DesktopItemKind.Shortcut), facts, nameCut: false));

        // A link whose target is not known says what a file says.
        var plain = DesktopTip.Item(Locale.En, Item("Site.url", DesktopItemKind.Shortcut), facts with { LinkTarget = null }, nameCut: false);
        Assert.StartsWith("Type: Shortcut", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void S5610_there_is_no_tip_while_the_name_is_whole_and_there_is_nothing_more_to_say()
    {
        // Not read yet, a shell item (no file), and an own icon whose name is its place.
        Assert.Null(DesktopTip.Item(Locale.En, Item("Budget.xlsx", DesktopItemKind.File), null, nameCut: false));
        Assert.Null(DesktopTip.Item(Locale.En, new DesktopItem("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem), new DesktopItemFacts("x", 1, Written, Written), nameCut: false));
        Assert.Null(DesktopTip.Own(Locale.En, "Documents", LaunchTargets.Classify("shell:Personal"), nameCut: false));
        Assert.Null(DesktopTip.Own(Locale.En, "Anything", default, nameCut: false));
    }

    [Fact]
    public void S5610_a_name_cut_short_leads_the_tip_in_full()
    {
        Assert.Equal("A very long name of a file.txt", DesktopTip.Item(Locale.En, Item("A very long name of a file.txt", DesktopItemKind.File), null, nameCut: true));
        Assert.Equal("My papers of the year\nLocation: Documents", DesktopTip.Own(Locale.En, "My papers of the year", LaunchTargets.Classify("shell:Personal"), nameCut: true));
    }

    [Theory]
    [InlineData("shell:Personal", "Location: Documents")]
    [InlineData("shell:RecycleBinFolder", "Location: Recycle Bin")]
    [InlineData("ms-settings:display", "Location: Display")]
    [InlineData("https://example.com/a", "Location: https://example.com/a")]
    [InlineData(@"C:\Tools\run.exe", @"Location: run.exe (C:\Tools)")]
    [InlineData(@"D:\run.exe", @"Location: run.exe (D:\)")]
    public void S5610_an_own_icon_says_its_location_by_name_never_a_raw_shell_or_settings_string(string target, string tip)
    {
        var said = DesktopTip.Own(Locale.En, "Mine", LaunchTargets.Classify(target), nameCut: false);

        Assert.Equal(tip, said);
        Assert.DoesNotContain("shell:", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ms-settings:", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void S5610_a_windows_location_with_no_name_of_its_own_says_nothing()
    {
        // Regression: the tip was the raw target under the name ("shell:Personal").
        Assert.Null(DesktopTip.Own(Locale.En, "Mine", LaunchTargets.Classify("shell:AppsFolder"), nameCut: false));
        Assert.Null(DesktopTip.Place(Locale.En, LaunchTargets.Classify("shell:::{645FF040-5081-101B-9F08-00AA002F954E}")));
    }
}
