using Themia.App.Shell;
using Themia.Core.Localization;

namespace Themia.Core.Settings;

/// <summary>[S-2300] The edit bar's pages, in the bar's order: each is an entry of the bar and the flyout it opens.</summary>
public enum SettingsPageId
{
    General,
    Accounts,
    Display,
    Views,
    Appearance,
}

/// <summary>
/// [S-2301] A section of a page: a heading (none for the page's first, which the entry names) over its
/// lines, which the page parts into cards at their breaks ([S-2304]). <see cref="Key"/> is the path of
/// the line it was built from (a submenu's), so a deep link finds it in every language.
/// </summary>
public sealed record SettingsSection(string Key, string? Heading, IReadOnlyList<SettingsLine> Lines);

/// <summary>[S-2300] A page of the edit bar: its sections, in order.</summary>
public sealed record SettingsPage(SettingsPageId Id, IReadOnlyList<SettingsSection> Sections)
{
    /// <summary>The bar entry's words.</summary>
    public string Name => SettingsPages.Name(Id);
}

/// <summary>
/// [S-2300]..[S-2306] The edit bar's five pages over the settings' lines, after Karpo's edit bar
/// (karpo-app, docs/widgets.md §575): <b>General</b> (General with Snap to grid, Language and formats,
/// Online updates, Motion, AI assistants, Back up and reset, Themia Pro, About Themia, Quit), <b>Accounts</b>,
/// <b>Display</b> (this display's), <b>Views</b> (the shown view's and every view's) and <b>Appearance</b>.
/// The lines stay what they are (<see cref="SettingsMenu"/>, <see cref="DisplayMenu"/>,
/// <see cref="ViewsMenu"/>): a page only groups them, one rule for all ([S-2301]), so every setting
/// has exactly one home ([S-2306]).
/// </summary>
public static class SettingsPages
{
    /// <summary>[S-2300] The pages in the bar's order.</summary>
    public static IReadOnlyList<SettingsPageId> Order { get; } = Enum.GetValues<SettingsPageId>();

    /// <summary>[S-2300] A page's entry words.</summary>
    public static string Name(SettingsPageId page) => page switch
    {
        SettingsPageId.General => Str.Common_General.Text(),
        SettingsPageId.Accounts => Str.Shell_Settings_Nav_Accounts.Text(),
        SettingsPageId.Display => Str.BarMenus_Display.Text(),
        SettingsPageId.Views => Str.Shell_ViewBar_Name.Text(),
        SettingsPageId.Appearance => Str.Settings_Section_Appearance.Text(),
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "not a page"),
    };

    /// <summary>[S-2300] A page's entry glyph: General the gear.</summary>
    public static string Glyph(SettingsPageId page) => page switch
    {
        SettingsPageId.General => Glyphs.Settings,
        SettingsPageId.Accounts => Glyphs.Contact,
        SettingsPageId.Display => Glyphs.Display,
        SettingsPageId.Views => Glyphs.View,
        SettingsPageId.Appearance => Glyphs.Palette,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "not a page"),
    };

    /// <summary>
    /// [S-2300] General: General's own lines (Snap to grid among them, [S-3516]), then Language and
    /// formats, Online updates, Motion, AI assistants (where a server runs), Back up and reset, Themia
    /// Pro and About Themia, each a section, and Quit Themia last.
    /// </summary>
    public static SettingsPage General(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        List<SettingsLine> lines = [.. SettingsMenu.General(host), SettingsMenu.Language(host), SettingsMenu.OnlineUpdates(host), SettingsMenu.Motion(host)];
        if (SettingsMenu.Assistants(host) is { } assistants)
        {
            lines.Add(assistants);
        }

        lines.AddRange([SettingsMenu.Backup(host), SettingsMenu.Pro(host), SettingsMenu.About(host)]);
        lines.Add(new SettingsCommand(SettingsMenu.Keys.Quit, Str.Settings_Quit_Label.Text(), Glyphs.Power, host.Quit));
        return new SettingsPage(SettingsPageId.General, Sectioned(lines, Str.Common_General.Text()));
    }

    /// <summary>[S-2300] Accounts: every account (a page of its own), then Add account.</summary>
    public static SettingsPage Accounts(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Accounts, Sectioned(SettingsMenu.Accounts(host).Lines, null));
    }

    /// <summary>[S-2300] Appearance: the style, then theme and colours, item background, the cards and text and size.</summary>
    public static SettingsPage Appearance(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Appearance, Sectioned(SettingsMenu.Appearance(host).Lines, null));
    }

    /// <summary>[S-2300] Display: the display this bar stands on, then what every display shares.</summary>
    public static SettingsPage Display(IDisplayMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Display, Sectioned(DisplayMenu.Root(host), null));
    }

    /// <summary>[S-2300] Views: the view shown here, then every view as the user's own things.</summary>
    public static SettingsPage Views(IViewsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Views, Sectioned(ViewsMenu.Root(host), null));
    }

    /// <summary>
    /// [S-2301] The one rule that makes a page of lines: a heading line starts a section under its
    /// words; a submenu that is not a page of its own is a section of its own (its words the heading, its
    /// lines the section's); every other line joins the section it follows, the first one headed
    /// <paramref name="first"/> (none: the entry names it). No section is empty.
    /// </summary>
    public static IReadOnlyList<SettingsSection> Sectioned(IReadOnlyList<SettingsLine> lines, string? first)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var sections = new List<SettingsSection>();
        var key = "page";
        string? heading = first;
        var loose = new List<SettingsLine>();

        void Close()
        {
            if (loose.Any(l => l is not SettingsSeparator))
            {
                sections.Add(new SettingsSection(key, heading, loose));
            }

            loose = [];
        }

        foreach (var line in lines)
        {
            switch (line)
            {
                case SettingsHeading head:
                    Close();
                    (key, heading) = (head.Key, head.Text);
                    break;
                case SettingsSubmenu { Page: false } submenu:
                    Close();
                    sections.Add(new SettingsSection(submenu.Key, submenu.Text, submenu.Lines));
                    (key, heading) = ("-" + submenu.Key, null);
                    break;
                default:
                    loose.Add(line);
                    break;
            }
        }

        Close();
        return sections;
    }

    /// <summary>
    /// [S-2306] [S-3534] Where the line at <paramref name="key"/> stands on <paramref name="page"/>: its
    /// section's key, then the keys of the rows down to it (an expander, a sub-page, a choice and the
    /// line itself); null where the page has no such line.
    /// </summary>
    public static IReadOnlyList<string>? PathTo(SettingsPage page, string key)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(key);

        foreach (var section in page.Sections)
        {
            if (section.Key == key)
            {
                return [section.Key];
            }

            if (Walk(section.Lines, key) is { } path)
            {
                return [section.Key, .. path];
            }
        }

        return null;
    }

    /// <summary>[S-2306] The line along <paramref name="path"/> (a section's key, then the rows' keys); null where it is not there.</summary>
    public static SettingsLine? Find(SettingsPage page, IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(path);

        return path.Count < 2 || page.Sections.FirstOrDefault(s => s.Key == path[0]) is not { } section
            ? null
            : SettingsMenu.Find(section.Lines, [.. path.Skip(1)]);
    }

    /// <summary>[S-2306] Every line of a page, depth first (the options of a choice and the lines of a submenu after it).</summary>
    public static IEnumerable<SettingsLine> Lines(SettingsPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.Sections.SelectMany(s => Flatten(s.Lines));
    }

    private static IEnumerable<SettingsLine> Flatten(IReadOnlyList<SettingsLine> lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            var below = line switch
            {
                SettingsSubmenu submenu => submenu.Lines,
                SettingsChoice choice => choice.Options,
                _ => (IReadOnlyList<SettingsLine>)[],
            };

            foreach (var inner in Flatten(below))
            {
                yield return inner;
            }
        }
    }

    private static List<string>? Walk(IReadOnlyList<SettingsLine> lines, string key)
    {
        foreach (var line in lines)
        {
            if (line.Key == key)
            {
                return [line.Key];
            }

            var below = line switch
            {
                SettingsSubmenu submenu => submenu.Lines,
                SettingsChoice choice => choice.Options,
                _ => null,
            };

            if (below is not null && Walk(below, key) is { } rest)
            {
                return [line.Key, .. rest];
            }
        }

        return null;
    }
}
