using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>[S-11702] One tile of the add page: what a click on it places.</summary>
public abstract record AddTile
{
    /// <summary>The tile's key, the same in every language: its picture's cache key and the keys' place.</summary>
    public abstract string Key { get; }
}

/// <summary>[S-11702] A widget of one kind, placed at its natural size.</summary>
public sealed record AddWidgetTile(WidgetId Kind) : AddTile
{
    public override string Key => "kind:" + Kind.Value;
}

/// <summary>[S-11702] [S-2473] A ready-made group: all its widgets placed as one new group.</summary>
public sealed record AddGroupTile(ReadyMadeGroup Group) : AddTile
{
    public override string Key => "group:" + Group.Id;
}

/// <summary>[S-5523] An unplaced desktop item: its widget placed, preset to it.</summary>
public sealed record AddItemTile(DesktopItem Item) : AddTile
{
    public override string Key => "item:" + Item.Key;
}

/// <summary>[S-11701] What a section of the add page holds.</summary>
public enum AddSectionKind
{
    /// <summary>Ready-made groups: Start's starters, a subject's own, a search's.</summary>
    ReadyMade,

    /// <summary>Start's Popular widgets.</summary>
    Popular,

    /// <summary>A subject's widgets outside a family, or a search's widgets.</summary>
    Widgets,

    /// <summary>One family of a subject, under its name.</summary>
    Family,

    /// <summary>[S-5523] This view's unplaced desktop items.</summary>
    Desktop,
}

/// <summary>[S-11701] A section of the add page: a heading over its tiles. Never empty.</summary>
/// <param name="Key">The same in every language: <c>tab/kind</c>, a family's <c>tab/family:Id</c>.</param>
public sealed record AddSection(string Key, AddSectionKind Kind, LocalText Heading, IReadOnlyList<AddTile> Tiles);

/// <summary>[S-11700] A tab of the add page: Start, a subject, or More.</summary>
public sealed record AddTab(AddFolderId Id, LocalText Title);

/// <summary>
/// [S-11700]..[S-11706] The add page, after Karpo's (<c>karpo-app</c>, docs/widgets.md §593, §578): tabs
/// of tiles, built from <see cref="AddTree"/> (the subjects, the families, the homes, Popular) and
/// <see cref="ReadyMadeGroups"/>, pure, so the page the app draws is a drawing of what this returns.
/// <b>Start</b> first (the starter groups, then Popular), then a tab per subject in Browse's order (its
/// ready-made groups, its widgets, each family under its name), then <b>More</b> while the tree leaves a
/// kind out. A search lists what matches from every tab, groups and widgets under a heading each.
/// </summary>
public sealed class AddPage
{
    private readonly Dictionary<AddFolderId, IReadOnlyList<AddSection>> _sections;

    private AddPage(IReadOnlyList<AddTab> tabs, Dictionary<AddFolderId, IReadOnlyList<AddSection>> sections, IReadOnlyList<AddHome> homes, IReadOnlyList<ReadyMadeGroup> groups)
    {
        Tabs = tabs;
        _sections = sections;
        Homes = homes;
        Groups = groups;
    }

    /// <summary>[S-11700] The tabs, in order: Start, the subjects, More where anything stands in it.</summary>
    public IReadOnlyList<AddTab> Tabs { get; }

    /// <summary>[S-3511] [S-3513] Every widget the page offers, once, at its home (Start's Popular repeats some).</summary>
    public IReadOnlyList<AddHome> Homes { get; }

    /// <summary>[S-2473] The ready-made groups the page offers, in <see cref="ReadyMadeGroups.All"/>' order.</summary>
    public IReadOnlyList<ReadyMadeGroup> Groups { get; }

    /// <summary>[S-11701] A tab's sections; none for a tab the page does not have.</summary>
    public IReadOnlyList<AddSection> Sections(AddFolderId tab) => _sections.GetValueOrDefault(tab) ?? [];

    /// <summary>
    /// [S-11700] The page for <paramref name="offered"/> kinds (the catalogue's, in its order) and the
    /// ready-made <paramref name="groups"/> this build can place. [S-5523] <paramref name="itemKind"/>, the
    /// desktop item kind, is never a tile of its own: the view's <paramref name="unplaced"/> items are, in
    /// a section of the subject it is at home in.
    /// </summary>
    public static AddPage Of(IReadOnlyList<WidgetId> offered, IReadOnlyList<ReadyMadeGroup> groups, WidgetId? itemKind = null, IReadOnlyList<DesktopItem>? unplaced = null)
        => Of(AddTree.Popular, AddTree.Browse, offered, groups, itemKind, unplaced);

    /// <summary>The page over a tree of its own (a test's part of the catalogue).</summary>
    internal static AddPage Of(AddShelf popular, AddShelf browse, IReadOnlyList<WidgetId> offered, IReadOnlyList<ReadyMadeGroup> groups, WidgetId? itemKind, IReadOnlyList<DesktopItem>? unplaced)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(groups);

        var have = new HashSet<WidgetId>(offered);
        if (itemKind is { } skip)
        {
            have.Remove(skip);
        }

        var tabs = new List<AddTab> { new(AddFolderId.Start, Str.AddMenu_Start.Local()) };
        var sections = new Dictionary<AddFolderId, IReadOnlyList<AddSection>>();
        var homes = new List<AddHome>();

        // [S-11703] Start: the starter groups in their order, then Popular.
        var start = new List<AddSection>();
        Add(start, AddFolderId.Start, AddSectionKind.ReadyMade, Str.Groups_Add_ReadyMade.Local(),
            [.. groups.Where(g => g.Starter is not null).OrderBy(g => g.Starter).Select(g => (AddTile)new AddGroupTile(g))]);
        Add(start, AddFolderId.Start, AddSectionKind.Popular, Str.AddMenu_Popular.Local(),
            [.. popular.Rows.OfType<AddLeaf>().Where(leaf => have.Contains(leaf.Kind)).Select(leaf => (AddTile)new AddWidgetTile(leaf.Kind))]);
        sections[AddFolderId.Start] = start;

        // [S-11704] A subject: its groups, its own widgets, each family under its name.
        var placedGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var subject in browse.Rows.OfType<AddShelf>())
        {
            var own = new List<AddTile>();
            var families = new List<AddSection>();
            foreach (var row in subject.Rows)
            {
                switch (row)
                {
                    case AddLeaf leaf when have.Contains(leaf.Kind):
                        own.Add(new AddWidgetTile(leaf.Kind));
                        homes.Add(new AddHome(leaf.Kind, [subject]));
                        break;
                    case AddShelf family:
                        var kinds = family.Rows.OfType<AddLeaf>().Where(leaf => have.Contains(leaf.Kind)).Select(leaf => leaf.Kind).ToList();

                        // A family of one is that widget, among the subject's own ([S-3511]).
                        if (kinds.Count == 1)
                        {
                            own.Add(new AddWidgetTile(kinds[0]));
                            homes.Add(new AddHome(kinds[0], [subject]));
                        }
                        else if (kinds.Count > 1)
                        {
                            families.Add(new AddSection($"{subject.Id}/family:{family.Id}", AddSectionKind.Family, family.Title, [.. kinds.Select(k => (AddTile)new AddWidgetTile(k))]));
                            homes.AddRange(kinds.Select(k => new AddHome(k, [subject, family])));
                        }

                        break;
                }
            }

            var mine = groups.Where(g => AddTree.SubjectOf(g.Home) == subject.Id).ToList();
            placedGroups.UnionWith(mine.Select(g => g.Id));

            // [S-5523] The view's unplaced desktop items where the item kind is at home.
            var items = itemKind is { } item && Holds(subject, item) && unplaced is { Count: > 0 }
                ? unplaced.Select(i => (AddTile)new AddItemTile(i)).ToList()
                : [];

            var list = new List<AddSection>();
            Add(list, subject.Id, AddSectionKind.ReadyMade, Str.Groups_Add_ReadyMade.Local(), [.. mine.Select(g => (AddTile)new AddGroupTile(g))]);
            Add(list, subject.Id, AddSectionKind.Widgets, Str.AddMenu_Widgets.Local(), own);
            Add(list, subject.Id, AddSectionKind.Desktop, Str.Desktop_Add_Folder.Local(), items);
            list.AddRange(families);
            if (list.Count > 0)
            {
                tabs.Add(new AddTab(subject.Id, subject.Title));
                sections[subject.Id] = list;
            }
        }

        // [S-3511] What the tree has no home for, and a group whose subject is not on the page: More.
        var homed = homes.Select(h => h.Kind).ToHashSet();
        var left = offered.Distinct().Where(kind => have.Contains(kind) && !homed.Contains(kind)).ToList();
        var stray = groups.Where(g => !placedGroups.Contains(g.Id)).ToList();
        if (left.Count > 0 || stray.Count > 0)
        {
            var more = new AddShelf(AddFolderId.More, Str.AddMenu_Subject_More.Local(), left.Count > 0 ? left[0] : browse.Icon, [.. left.Select(k => (AddNode)new AddLeaf(k))]);
            var list = new List<AddSection>();
            Add(list, AddFolderId.More, AddSectionKind.ReadyMade, Str.Groups_Add_ReadyMade.Local(), [.. stray.Select(g => (AddTile)new AddGroupTile(g))]);
            Add(list, AddFolderId.More, AddSectionKind.Widgets, Str.AddMenu_Widgets.Local(), [.. left.Select(k => (AddTile)new AddWidgetTile(k))]);
            homes.AddRange(left.Select(k => new AddHome(k, [more])));
            tabs.Add(new AddTab(AddFolderId.More, more.Title));
            sections[AddFolderId.More] = list;
        }

        return new AddPage(tabs, sections, homes, groups);
    }

    /// <summary>Add a section of <paramref name="tiles"/> unless it would be empty.</summary>
    private static void Add(List<AddSection> sections, AddFolderId tab, AddSectionKind kind, LocalText heading, IReadOnlyList<AddTile> tiles)
    {
        if (tiles.Count > 0)
        {
            sections.Add(new AddSection($"{tab}/{kind}", kind, heading, tiles));
        }
    }

    /// <summary>Whether <paramref name="kind"/> is at home in <paramref name="shelf"/> or a family of it.</summary>
    private static bool Holds(AddShelf shelf, WidgetId kind)
        => shelf.Rows.Any(row => row is AddLeaf { Again: false } leaf && leaf.Kind == kind || row is AddShelf inner && Holds(inner, kind));

    // ---- [S-11706] the search -----------------------------------------------------------------

    /// <summary>
    /// The search index, built once per language and page shape: the names change with the language
    /// ([S-1109]). Built on first use, so opening the page never pays for it.
    /// </summary>
    private (Locale Locale, (IndexedWidget Index, AddTile Tile)[] Entries)? _index;

    /// <summary>[S-11706] Whether <paramref name="query"/> searches at all (spaces alone do not).</summary>
    public static bool Searching(string? query) => WidgetSearch.Tokenize(query).Length > 0;

    /// <summary>
    /// [S-11706] [S-3513] [S-2473] What a search shows in place of the tab: the ready-made groups and the
    /// widgets that match, from every tab, each under one heading, best first. Each widget once, at its
    /// home, matched on its name, keywords, kind, description and the names of its subject and family;
    /// each group on its name, what it is about and the widgets it holds. Nothing matching: no section.
    /// </summary>
    public IReadOnlyList<AddSection> Find(string? query)
    {
        if (!Searching(query))
        {
            return [];
        }

        var locale = Strings.Current;
        if (_index is not { } built || built.Locale != locale)
        {
            built = (locale,
            [
                .. Groups.Select(g => (WidgetSearch.Index(AddSearch.Doc(g)), (AddTile)new AddGroupTile(g))),
                .. Homes.Select(h => (WidgetSearch.Index(AddSearch.Doc(ModuleCatalog.Of(h.Kind), h.Path)), (AddTile)new AddWidgetTile(h.Kind))),
            ]);
            _index = built;
        }

        var ranked = WidgetSearch.Rank(built.Entries.Select(e => (e.Index, e.Tile)), query);
        var found = new List<AddSection>();
        Add(found, AddFolderId.Browse, AddSectionKind.ReadyMade, Str.Groups_Add_ReadyMade.Local(), [.. ranked.OfType<AddGroupTile>()]);
        Add(found, AddFolderId.Browse, AddSectionKind.Widgets, Str.AddMenu_Widgets.Local(), [.. ranked.OfType<AddWidgetTile>()]);
        return found;
    }
}

/// <summary>
/// [S-943] [S-3513] [S-2473] What the add page's search matches a widget or a ready-made group on. A
/// widget's name, description and folders (<paramref name="path"/>) come in the current language and,
/// when that is not English, in English as well — a German user who types "weather" still finds the
/// Wetter widget —, and the curated keywords (<see cref="WidgetDescriptor.Keywords"/>, English only)
/// keep working in every language.
/// </summary>
public static class AddSearch
{
    /// <summary>[S-943] A widget at its home.</summary>
    public static WidgetSearchDoc Doc(WidgetDescriptor definition, IReadOnlyList<AddShelf> path)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(path);

        var locale = Strings.Current;
        return new WidgetSearchDoc(
            definition.Id.Value,
            Both(locale, definition.Label),
            definition.Keywords,
            Both(definition.Describe(locale), definition.Describe(Locale.En)),
            [.. path.SelectMany(shelf => Both(locale, shelf.Title))],
            [.. definition.Providers.Select(p => p.Value)]);
    }

    /// <summary>[S-2473] A ready-made group: its name, what it is about, the names of the widgets it places, "Ready-made".</summary>
    public static WidgetSearchDoc Doc(ReadyMadeGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var locale = Strings.Current;
        var kinds = group.Widgets
            .Select(w => WidgetId.TryParse(w.Kind, out var id) && ModuleCatalog.TryGet(id, out var descriptor) ? descriptor : null)
            .OfType<WidgetDescriptor>()
            .ToList();

        // [S-3513] Its subject's name too, as a widget's: "music" finds Now playing.
        var subject = AddTree.SubjectOf(group.Home) is { } id ? AddTree.Browse.Rows.OfType<AddShelf>().FirstOrDefault(s => s.Id == id) : null;
        return new WidgetSearchDoc(
            "group:" + group.Id,
            Both(locale, group.Name.Local()),
            null,
            [.. Both(locale, group.About.Local()), .. kinds.SelectMany(k => Both(locale, k.Label))],
            [.. Both(locale, Str.Groups_Add_ReadyMade.Local()), .. subject is null ? [] : Both(locale, subject.Title)]);
    }

    private static string[] Both(Locale locale, LocalText text) => Both(text.Text(locale), text.Text(Locale.En));

    /// <summary>The text shown, then in English when that differs; the first is the shown one.</summary>
    private static string[] Both(string shown, string english)
        => string.Equals(shown, english, StringComparison.Ordinal) ? [shown] : [shown, english];
}
