using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Settings;

namespace Themia.App.Shell;

/// <summary>
/// What one settings row is, kept on the row's frame for search. [S-1540], [S-1542]
/// <para>
/// Every row any page builds goes through <see cref="FluentLayout"/>, which draws it as a Windows 11
/// <see cref="SettingsCard"/> (or <see cref="SettingsExpander"/>) inside a <see cref="Border"/>
/// frame and hangs one of these on the frame's <c>Tag</c>. The window reads them back off the
/// built page — so search is a registry of the rows that exist, not a list kept beside the pages
/// that could drift. A row added to a page is searchable the moment it exists.
/// </para>
/// <para>
/// The frame stays a plain <c>Border</c> (WinUI seals it), so the dozens of pages that hold a row
/// as a <c>Border</c> — to show and hide it, to hoist a danger row — keep working unchanged. This
/// record holds the card and the control but never the frame, so nothing here keeps a page alive
/// across the managed/native boundary.
/// </para>
/// </summary>
public sealed class SettingRow(string title, string? description, FrameworkElement? control)
{
    /// <summary>The row's title, as the card shows it and as search finds it.</summary>
    public string Title { get; } = title;

    public string? Description { get; } = description;

    /// <summary>The control on the right, read for keywords (a drop-down's options) at index time.</summary>
    public FrameworkElement? Control { get; } = control;

    /// <summary>Extra words that find the row without being on it.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>The card a search result rings when it lands here.</summary>
    public Control? Card { get; init; }

    /// <summary>The rows folded inside this one, when it is an expander.</summary>
    public IReadOnlyList<SettingRow> Nested { get; init; } = [];

    /// <summary>The expander this row is folded into, so landing on it can open it first.</summary>
    public SettingsExpander? Parent { get; set; }

    /// <summary>
    /// [S-1832] On a list page, the display or view whose group this row is (or is in); null for a
    /// row of the page itself. Search lands on the row in that group, not its namesake in another.
    /// </summary>
    public string? Item { get; set; }

    /// <summary>The record on a row's frame, or null for anything that is not a settings row.</summary>
    public static SettingRow? Of(FrameworkElement? element) => element is Border { Tag: SettingRow row } ? row : null;

    /// <summary>Every word search should know this row by: its own keywords and its drop-down's options.</summary>
    public IReadOnlyList<string> AllKeywords()
    {
        var words = new List<string>(Keywords);

        if (Control is ComboBox combo)
        {
            foreach (var item in combo.Items)
            {
                if (item is ComboBoxItem { Content: string label })
                {
                    words.Add(label);
                }
                else if (item is string text)
                {
                    words.Add(text);
                }
            }
        }

        return words;
    }
}

/// <summary>
/// Reads the rows back off a built page, in page order, each with the section heading above it.
/// </summary>
public static class SettingsIndex
{
    /// <summary>The marker a section heading carries, so the walk can tell it from any other text.</summary>
    internal const string SectionMark = "themia.section";

    /// <summary>Every row on the page and the section it sits under, top to bottom.</summary>
    public static IReadOnlyList<(SettingRow Row, string? Section)> Collect(FrameworkElement page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var found = new List<(SettingRow, string?)>();
        string? section = null;
        Walk(page, found, ref section);
        return found;
    }

    /// <summary>The page's rows as search entries, under one page key and path.</summary>
    public static IEnumerable<SettingEntry> Entries(FrameworkElement page, string pageKey, string pagePath)
    {
        foreach (var (row, section) in Collect(page))
        {
            yield return new SettingEntry(pageKey, pagePath, section, row.Title, row.Description, row.AllKeywords());
        }
    }

    private static void Walk(DependencyObject node, List<(SettingRow, string?)> found, ref string? section)
    {
        switch (node)
        {
            case TextBlock { Tag: string mark } heading when mark == SectionMark:
                section = heading.Text;
                return;

            case Border { Tag: SettingRow row } frame:
                if (frame.Visibility == Visibility.Visible)
                {
                    found.Add((row, section));

                    foreach (var nested in row.Nested)
                    {
                        found.Add((nested, section));
                    }
                }

                return;

            case Panel panel:
                if (panel.Visibility != Visibility.Visible)
                {
                    return;
                }

                foreach (var child in panel.Children)
                {
                    Walk(child, found, ref section);
                }

                return;

            case Border { Child: { } child } border when border.Visibility == Visibility.Visible:
                Walk(child, found, ref section);
                return;

            case ContentControl { Content: DependencyObject content }:
                Walk(content, found, ref section);
                return;
        }
    }
}
