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

    /// <summary>[S-13700] The desktop's icons the view shown on the bar's display does not place.</summary>
    DesktopItems,
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
/// [S-2300]..[S-2306] [S-13700] The edit bar's six pages over the settings' lines, after Karpo's edit bar
/// (karpo-app, docs/widgets.md §575): <b>General</b> ([S-2315]), <b>Accounts</b> ([S-2312]), <b>Display</b>
/// (every display a card, [S-2314]), <b>Views</b> (every view a card, [S-2313]), <b>Unplaced icons</b> ([S-13700]) and <b>Appearance</b> ([S-2316]).
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
        SettingsPageId.DesktopItems => Str.Unplaced_Page_Name.Text(),
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
        SettingsPageId.DesktopItems => Glyphs.DesktopIcons,
        SettingsPageId.Appearance => Glyphs.Palette,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "not a page"),
    };

    /// <summary>
    /// [S-2315] General, in Karpo's sections (karpo-app, Karpo.Core/KarpoMenu.cs <c>Rows</c>, docs/widgets.md §517,
    /// §579): under <b>General</b> Start with Windows, the Language and its Formats card and Pause during
    /// full-screen apps (no Quit Themia: [S-3532] retired, as Karpo's Quit Karpo); under <b>Desktop icons</b>
    /// Themia's own desktop's; under <b>Services</b> Online updates, a card that opens; under <b>Themia</b> Themia
    /// Pro, Back up &amp; reset and Uninstall Themia, cards too; then <b>About Themia</b>, a section of its own
    /// ([S-3580]). Motion is the Appearance's ([S-2316], Karpo's §682).
    /// </summary>
    public static SettingsPage General(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var general = SettingsMenu.General(host).ToList();
        var at = general.FindIndex(l => l.Key == SettingsMenu.Keys.GameMode);
        general.InsertRange(at < 0 ? general.Count : at, SettingsMenu.Language(host).Lines);

        List<SettingsLine> lines =
        [
            .. general,
            new SettingsHeading(SettingsMenu.Keys.DesktopHeading, Str.SettingsPages_General_Desktop.Text()),
            .. SettingsMenu.Desktop(host),
            new SettingsHeading(SettingsMenu.Keys.ServicesHeading, Str.SettingsPages_General_Services.Text()),
            SettingsMenu.OnlineUpdates(host) with { Card = true },
            new SettingsHeading(SettingsMenu.Keys.ThemiaHeading, Str.SettingsPages_General_Themia.Text()),
            SettingsMenu.Pro(host) with { Card = true },
            SettingsMenu.Backup(host) with { Card = true },
            SettingsMenu.Uninstall(host) with { Card = true },
            SettingsMenu.About(host),
        ];
        return new SettingsPage(SettingsPageId.General, Sectioned(lines, Str.Common_General.Text()));
    }

    /// <summary>[S-2312] Accounts: under "Your accounts" every account's card, then under "Add an account" its ways.</summary>
    public static SettingsPage Accounts(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Accounts, Sectioned(SettingsMenu.Accounts(host).Lines, null));
    }

    /// <summary>
    /// [S-2316] Appearance, after Karpo's (karpo-app, docs/widgets.md §592, §613, §671, §682): the live preview first
    /// and bare ([S-2311]), the style, then theme and colours, item background, the cards, text and size, and Motion
    /// (Karpo's Animations, the Appearance's since its §682).
    /// </summary>
    public static SettingsPage Appearance(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Appearance, Sectioned(
            [new SettingsPreview(SettingsMenu.Keys.Preview), .. SettingsMenu.Appearance(host).Lines, SettingsMenu.Motion(host)], null));
    }

    /// <summary>[S-2314] Display: every display's card, then what every display shares.</summary>
    public static SettingsPage Display(IDisplayMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Display, Sectioned(DisplayMenu.Root(host), null));
    }

    /// <summary>[S-2313] Views: every view's card, then Add a view.</summary>
    public static SettingsPage Views(IViewsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new SettingsPage(SettingsPageId.Views, Sectioned(ViewsMenu.Root(host), null));
    }

    /// <summary>
    /// [S-13707] Whether <paramref name="page"/>'s flyout is as wide as the bar, as the add page's ([S-11707]), rather than the
    /// pages' 580: the Unplaced icons page, whose icons stand in rows across it as on the desktop (owner, 2026-11-19: "the on
    /// desktop not on home view should be as wide as the edit bar").
    /// </summary>
    public static bool BarWide(SettingsPageId page) => page == SettingsPageId.DesktopItems;

    /// <summary>
    /// [S-13701] Whether a page's entry shows its icon alone at every width (its words the tooltip): Unplaced icons, an
    /// icon with its count badge (owner, 2026-11-19: "own entry in the topbar"), so the bar keeps its words on the
    /// displays it kept them on before it.
    /// </summary>
    public static bool IconAlone(SettingsPageId page) => page == SettingsPageId.DesktopItems;

    /// <summary>[S-13700] The Unplaced icons page's one line: the list of the view's unplaced desktop icons.</summary>
    public const string UnplacedKey = "unplaced";

    /// <summary>
    /// [S-13700] Unplaced icons: one live part, the page host's (the list of the desktop's icons the view shown on its
    /// display does not place, its search and its actions, <see cref="Desktop.UnplacedItems"/>), as the Appearance's
    /// preview is ([S-2311]). It sets nothing.
    /// </summary>
    public static SettingsPage DesktopItems()
        => new(SettingsPageId.DesktopItems, Sectioned([new SettingsPreview(UnplacedKey) { Name = Str.Unplaced_Page_Name.Text() }], null));

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
                case SettingsSubmenu { Page: false, Card: false } submenu:
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
