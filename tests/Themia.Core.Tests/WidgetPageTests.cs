using System.Text.RegularExpressions;
using Themia.Core.Settings;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6536] [S-1750] [S-1752] A widget's settings page holds only the widget's own options: what the
/// properties panel sets has no row there, and a page left with headings alone is no page. The page
/// builder is UI code, so the panel rule is checked against its source.
/// </summary>
public sealed partial class WidgetPageTests
{
    private static readonly string Config = RepoFiles.AppConfig;

    /// <summary>
    /// The panel's own words and writers: its row names (Title, Style, Header, Background, Item
    /// background, Item text, Scale, Alignment) and what writes a style, a scale or an alignment.
    /// </summary>
    [GeneratedRegex(@"Str\.(Group_Title_Label|Styles_Label|App_ConfigHeaderLabel|Settings_Style_BgLabel|Shell_Items_Background|Shell_Items_TextShort|Widget_Scale_Label|Widget_Align_Label)\b|WidgetStyles\.Set\(|WidgetScale\.|QuickSet\.SetAlign\(|\.Align\s*=")]
    private static partial Regex PanelProperty();

    private static IEnumerable<string> PageSources()
        => Directory.GetFiles(Config, "WidgetSettings*.cs").Append(Path.Combine(Config, "ConfigPages.Widget.cs"))
            .Concat(RepoFiles.WidgetModules().SelectMany(RepoFiles.CSharp).Where(f => File.ReadAllText(f).Contains("SettingsRows(", StringComparison.Ordinal)));

    [Fact]
    public void S6536_no_widget_settings_page_builds_a_row_the_properties_panel_sets()
    {
        var found = PageSources()
            .SelectMany(path => PanelProperty().Matches(File.ReadAllText(path)).Select(m => $"{Path.GetFileName(path)}: {m.Value}"))
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void S1750_whether_a_kind_has_a_page_is_read_off_the_rows_the_page_shows()
    {
        var source = File.ReadAllText(Path.Combine(Config, "ConfigPages.Widget.cs"));

        // The page is the rows WidgetRows makes and nothing added after them, so "has rows" there is
        // "has rows" on the page.
        var page = Regex.Match(source, @"private static SettingsPage WidgetPage\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(page.Success, "WidgetPage not found");
        Assert.Contains("WidgetRows(", page.Groups["body"].Value, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"rows\.(Add|Insert|AddRange|InsertRange)\(", page.Groups["body"].Value);

        // The rows are pruned of empty sections ([S-1752]) before anyone counts them.
        Assert.Contains("PageSections.Keep", source, StringComparison.Ordinal);
    }

    [Fact]
    public void S1752_a_heading_with_nothing_under_it_is_dropped()
        => Assert.Equal([1, 2, 3, 4], PageSections.Keep([true, true, false, true, false, true]));

    [Fact]
    public void S1752_the_rows_under_a_heading_keep_it_and_rows_before_any_heading_stay()
        => Assert.Equal([0, 1, 2, 3], PageSections.Keep([false, true, false, false]));

    [Fact]
    public void S1752_a_page_of_headings_alone_has_no_rows()
    {
        Assert.Empty(PageSections.Keep([true]));
        Assert.Empty(PageSections.Keep([true, true]));
        Assert.Empty(PageSections.Keep([]));
    }

    [Fact]
    public void S1752_a_trailing_heading_is_dropped()
        => Assert.Equal([0, 1], PageSections.Keep([true, false, true]));
}
