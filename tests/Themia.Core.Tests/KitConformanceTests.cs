using System.Text.RegularExpressions;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Kit rules read from the source of every place a widget's view can live: the app's widgets, the
/// SDK's widget parts and every widget module (<see cref="RepoFiles.ViewSources"/>), so a widget that
/// moves to its own project is held to them as before.
/// </summary>
public sealed partial class KitConformanceTests
{
    [GeneratedRegex(@"\bnew\s+(MenuFlyout|CommandBarFlyout)\b(?!\w)")]
    private static partial Regex BuildsAMenu();

    [GeneratedRegex(@"\b(Color|ColorHelper)\.FromArgb\(")]
    private static partial Regex LiteralColour();

    /// <summary>
    /// [S-822] Today's views that still make a colour from numbers (an avatar's initials, a chart's
    /// own palette, a fade): the list may only shrink. A view takes its colours from the theme
    /// (<c>ThemeBrush</c>), the item layer (<c>ItemFill</c>) or the user's own choice. Not an
    /// offender but on the list for the same match: <c>SelectionChrome.cs</c>, the one place the
    /// selection's translucent marks are made from Windows' highlight colour, by
    /// <c>SelectionLook</c>'s tested rule ([S-6627]).
    /// </summary>
    private static readonly string[] LiteralColourToday =
    [
        "CalendarView.cs", "ClipboardView.cs", "ContactsView.cs", "Glides.cs", "ItemPalette.cs", "MarkdownEditor.cs",
        "MusicView.Layout.cs", "ScratchpadView.cs", "ScreenTimeShared.cs", "SelectionChrome.cs", "StocksView.cs", "WeekPlannerView.cs",
    ];

    [GeneratedRegex(@"\bProcess\.Start\b|\bUseShellExecute\b")]
    private static partial Regex StartsAProcess();

    /// <summary>
    /// [S-7062] The files that start a process themselves, each for a reason: the one launcher
    /// (<c>ShellLauncher</c>), the purge's helper after exit, git for a folder's status, a file the
    /// shell's own operations open, and the settings window's About and uninstall pages.
    /// </summary>
    private static readonly string[] StartsAProcessAllowed =
    [
        "ConfigPages.About.cs", "ConfigPages.Uninstall.cs", "DataPurge.cs", "GitStatus.cs", "ShellFileOps.cs", "ShellLauncher.cs",
    ];

    private static string Code(string path)
        => string.Join('\n', File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    [Fact]
    public void S1740_no_widget_builds_a_menu_but_ItemMenu()
    {
        var offenders = RepoFiles.ViewSources()
            .Where(f => Path.GetFileName(f) != "ItemMenu.cs" && BuildsAMenu().IsMatch(Code(f)))
            .Select(f => Path.GetRelativePath(RepoFiles.Root, f))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void S822_no_widget_makes_a_colour_from_numbers_beyond_todays_list()
    {
        var offending = RepoFiles.ViewSources()
            .Where(f => LiteralColour().IsMatch(Code(f)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Exactly the list: a new offender fails, and a fixed one must leave the list.
        Assert.Equal(LiteralColourToday.Order(StringComparer.Ordinal), offending);
    }

    /// <summary>
    /// [S-7062] One launcher: nothing but the host's launcher hands a target to the shell; a widget
    /// opens through <c>IWidgetHost.Launcher</c>. The list may only shrink.
    /// </summary>
    [Fact]
    public void S7062_nothing_starts_a_process_but_the_launcher_and_todays_list()
    {
        var offending = RepoFiles.CSharp(RepoFiles.Src)
            .Where(f => StartsAProcess().IsMatch(Code(f)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(StartsAProcessAllowed.Order(StringComparer.Ordinal), offending);
    }

    [GeneratedRegex(@"\bFetchState\.(Freshness|IsStale)\(")]
    private static partial Regex SaysTheAge();

    /// <summary>
    /// [S-7042] The one stale rule: no widget writes its data's age itself (in its header, in the
    /// caution colour); a problem is the kit header's glyph and the age is on the Refresh line
    /// (<c>KitFreshness</c>).
    /// </summary>
    [Fact]
    public void S7042_no_widget_says_the_age_of_its_data_itself()
    {
        var offending = RepoFiles.ViewSources()
            .Where(f => SaysTheAge().IsMatch(Code(f)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offending);
    }

    /// <summary>
    /// [S-7065] One open gesture (decided, Q1/Q2): in every list of items a click focuses a row and a
    /// double-click (or Enter) opens it, the kit's list and the lists that follow it. The kit's list
    /// and Recent files check no edit mode for it: the card makes its widget inert there ([S-1751]).
    /// </summary>
    [Theory]
    [InlineData("KitList.cs", true)]
    [InlineData("RecentFilesView.cs", true)]
    [InlineData("NotificationsView.cs", false)]
    public void S7065_a_list_row_opens_on_a_double_click_and_a_click_only_focuses(string file, bool leavesEditModeToTheCard)
    {
        var code = Code(RepoFiles.Source(file));

        Assert.Matches(@"\.DoubleTapped \+=", code);
        if (leavesEditModeToTheCard)
        {
            Assert.DoesNotMatch(@"\bEditMode\b", code);
        }
    }

    [Fact]
    public void RepoFiles_view_sources_reach_the_sdk_and_the_modules()
    {
        var files = RepoFiles.ViewSources().Select(f => Path.GetRelativePath(RepoFiles.Src, f).Replace(Path.DirectorySeparatorChar, '/')).ToList();

        Assert.Contains(files, f => f.StartsWith("Themia.App/Widgets/", StringComparison.Ordinal));
        Assert.Contains(files, f => f.StartsWith("Themia.Sdk.WinUI/Widgets/", StringComparison.Ordinal));
        Assert.Contains(files, f => f.StartsWith("Widgets/Themia.Widget.Clock/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => f.Contains("/Tests/", StringComparison.Ordinal) || f.Contains("/obj/", StringComparison.Ordinal));
    }
}
