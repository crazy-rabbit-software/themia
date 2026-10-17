using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>One row of the declared tree: a widget, a folder of rows, or a line.</summary>
public abstract record AddNode;

/// <summary>
/// A widget. <paramref name="Again"/>: shown here once more, its home being elsewhere in the tree
/// (Popular's rows, [S-3511]); search lists it at its home only.
/// </summary>
public sealed record AddLeaf(WidgetId Kind, bool Again = false) : AddNode;

/// <summary>A line between two runs of a folder's rows.</summary>
public sealed record AddLine : AddNode;

/// <summary>A folder: a row opening a submenu of its rows; at the top, a column under its heading.</summary>
/// <param name="Icon">The kind whose icon its row wears; ignored for a top column, which is a heading.</param>
public sealed record AddShelf(AddFolderId Id, Str Title, WidgetId Icon, IReadOnlyList<AddNode> Rows) : AddNode;

/// <summary>A row of the tree as the menu shows it: a pick, a folder, or a line.</summary>
public abstract record AddRow;

/// <summary>A row that adds <see cref="Kind"/>; <see cref="Again"/> as <see cref="AddLeaf.Again"/>.</summary>
public sealed record AddPick(WidgetId Kind, bool Again) : AddRow;

/// <summary>A folder as the menu shows it: always two rows or more.</summary>
public sealed record AddFolder(AddShelf Shelf, IReadOnlyList<AddRow> Rows) : AddRow;

/// <summary>A line as the menu shows it: always between two rows.</summary>
public sealed record AddRule : AddRow;

/// <summary>One of the menu's top columns: its heading's shelf and its rows.</summary>
public sealed record AddColumn(AddShelf Shelf, IReadOnlyList<AddRow> Rows);

/// <summary>A widget's home in the tree: the folders it is in under its top column, outermost first.</summary>
public sealed record AddHome(WidgetId Kind, IReadOnlyList<AddShelf> Path);

/// <summary>
/// [S-3510..S-3512] The add menu's tree, after Karpo's add list (<c>karpo-app</c>, docs/widgets.md
/// §136): the menu asks the one question a person adding a widget has — what do I want? — and
/// answers it by subject. Two top columns, <b>Popular</b> (a fixed handful, each again from its home)
/// and <b>Browse</b> (a row per subject, opening its submenu as Windows' "New ›" does); inside a
/// subject a <b>family</b> — one widget in several layouts or parts (the calendar's five layouts,
/// the visualizer's five designs, a reading's meter and its details, the seven power buttons) — is
/// one row opening its own submenu. Never deeper than that.
/// <para>
/// The folders are declared here; each widget places itself in one through its descriptor
/// (<see cref="WidgetDescriptor.Add"/>, [S-6907]). <see cref="On"/> resolves the tree against what
/// the catalogue offers, so a kind the tree has no home for is still listed, in
/// <see cref="AddFolderId.More"/>, while <c>AddMenuTests</c> fail until it has one.
/// </para>
/// </summary>
public static class AddTree
{
    /// <summary>[S-3512] The most rows a column or a folder holds, a line counted as a row; Quick settings alone is longer.</summary>
    public const int MostRows = 12;

    private static readonly AddLine Line = new();

    /// <summary>A folder of the tree: its heading and where it stands among its parent's rows (a line parts two runs).</summary>
    private sealed record Folder(AddFolderId Id, Str Title, AddFolderId Parent, int Group, int Rank);

    /// <summary>
    /// [S-3511] The tree's folders: the subjects under Browse, in the order a person looks for them,
    /// and the families inside a subject (a reading's meter and its details, the screen time parts,
    /// the power buttons, every quick setting). The folders are the host's; the widgets place
    /// themselves in them ([S-6907], <see cref="AddPlacement"/>), so this names no widget.
    /// </summary>
    private static readonly Folder[] Folders =
    [
        new(AddFolderId.Time, Str.AddMenu_Subject_Time, AddFolderId.Browse, 0, 0),
        new(AddFolderId.CalendarMail, Str.AddMenu_Subject_Calendar, AddFolderId.Browse, 0, 1),
        new(AddFolderId.Weather, Str.AddMenu_Subject_Weather, AddFolderId.Browse, 0, 2),
        new(AddFolderId.Markets, Str.AddMenu_Subject_Markets, AddFolderId.Browse, 0, 3),
        new(AddFolderId.Media, Str.AddMenu_Subject_Media, AddFolderId.Browse, 0, 4),
        new(AddFolderId.Notes, Str.AddMenu_Subject_Notes, AddFolderId.Browse, 0, 5),
        new(AddFolderId.Files, Str.AddMenu_Subject_Files, AddFolderId.Browse, 0, 6),
        new(AddFolderId.System, Str.AddMenu_Subject_System, AddFolderId.Browse, 0, 7),
        new(AddFolderId.Processor, Str.WidgetsA_Catalog_Cpu_Label, AddFolderId.System, 0, 0),
        new(AddFolderId.MemoryReading, Str.WidgetsA_Catalog_Memory_Label, AddFolderId.System, 0, 1),
        new(AddFolderId.Graphics, Str.WidgetsA_Catalog_Gpu_Label, AddFolderId.System, 0, 2),
        new(AddFolderId.NetworkReading, Str.WidgetsA_Catalog_Network_Label, AddFolderId.System, 0, 4),
        new(AddFolderId.PingReading, Str.Widget_Ping_Label, AddFolderId.System, 0, 5),
        new(AddFolderId.ScreenTime, Str.AddMenu_Family_ScreenTime, AddFolderId.System, 1, 0),
        new(AddFolderId.Devices, Str.AddMenu_Subject_Devices, AddFolderId.Browse, 0, 8),
        new(AddFolderId.BatteryReading, Str.Widget_Battery_Label, AddFolderId.Devices, 0, 0),
        new(AddFolderId.PowerButtons, Str.AddMenu_Family_PowerButtons, AddFolderId.Devices, 0, 1),
        new(AddFolderId.QuickSettings, Str.Toggles_Category, AddFolderId.Browse, 0, 9),
    ];

    private static readonly Lazy<(AddShelf Popular, AddShelf Browse)> Tree = new(Build);

    /// <summary>
    /// [S-3511] The first column: what most people add first, each again where it is at home, in the
    /// order of how often it is wanted (each widget's <see cref="AddPlacement.Popular"/>).
    /// </summary>
    public static AddShelf Popular => Tree.Value.Popular;

    /// <summary>
    /// [S-3511] The second column: every subject, a folder each. Inside each, the most wanted first.
    /// </summary>
    public static AddShelf Browse => Tree.Value.Browse;

    /// <summary>
    /// [S-3511] Every toggle, as Windows' quick settings have them, lines parting them by subject. The
    /// one folder longer than <see cref="MostRows"/>, since it is every toggle and a person looks for
    /// one of them by its name.
    /// </summary>
    public static AddShelf QuickSettings => Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == AddFolderId.QuickSettings);

    /// <summary>The menu's top: its two columns, left to right.</summary>
    public static IReadOnlyList<AddShelf> Top => [Popular, Browse];

    private static (AddShelf Popular, AddShelf Browse) Build()
    {
        var widgets = ModuleCatalog.Widgets;
        AddLeaf[] popular = [.. widgets.Where(w => w.Add.Popular is not null).OrderBy(w => w.Add.Popular).Select(w => new AddLeaf(w.Id, Again: true))];
        return (new AddShelf(AddFolderId.Popular, Str.AddMenu_Popular, popular[0].Kind, popular), Shelf(AddFolderId.Browse, Str.AddMenu_Browse, widgets));
    }

    /// <summary>A folder's rows: its widgets and its folders, by run and place, a line between two runs.</summary>
    private static AddShelf Shelf(AddFolderId id, Str title, IReadOnlyList<WidgetDescriptor> widgets)
    {
        var entries = widgets.Where(w => w.Add.Shelf == id).Select(w => (w.Add.Group, w.Add.Rank, Node: (AddNode)new AddLeaf(w.Id)))
            .Concat(Folders.Where(f => f.Parent == id).Select(f => (f.Group, f.Rank, Node: (AddNode)Shelf(f.Id, f.Title, widgets))))
            .OrderBy(e => e.Group).ThenBy(e => e.Rank);

        var rows = new List<AddNode>();
        int? run = null;
        foreach (var (group, _, node) in entries)
        {
            if (run is { } last && group != last)
            {
                rows.Add(Line);
            }

            run = group;
            rows.Add(node);
        }

        return new AddShelf(id, title, IconOf(rows), rows);
    }

    /// <summary>A folder's row wears its first widget's icon.</summary>
    private static WidgetId IconOf(IReadOnlyList<AddNode> rows) => rows.FirstOrDefault(row => row is not AddLine) switch
    {
        AddLeaf leaf => leaf.Kind,
        AddShelf shelf => shelf.Icon,
        _ => throw new InvalidOperationException("an add menu folder with no widget in it"),
    };

    /// <summary>Where what the tree leaves out stands meanwhile ([S-3511]); its rows are filled by <see cref="On"/>.</summary>
    private static AddShelf More => new(AddFolderId.More, Str.AddMenu_Subject_More, Browse.Icon, []);

    /// <summary>
    /// [S-3511] What the menu shows of the tree for the kinds the catalogue offers, in the
    /// catalogue's order: each top column with its rows, a folder only where it keeps two rows or more
    /// (one is that row; none, no folder), a line only between two rows. An offered kind the tree has
    /// no home for stands in <see cref="AddFolderId.More"/>, at Browse's end.
    /// </summary>
    public static IReadOnlyList<AddColumn> On(IReadOnlyList<WidgetId> offered)
    {
        ArgumentNullException.ThrowIfNull(offered);

        var have = new HashSet<WidgetId>(offered);
        var placed = new HashSet<WidgetId>();
        var columns = new List<AddColumn>();

        foreach (var shelf in Top)
        {
            var rows = Resolve(shelf.Rows, have, placed);
            if (shelf.Id == AddFolderId.Browse)
            {
                var left = offered.Distinct().Where(kind => !placed.Contains(kind)).Select(kind => (AddRow)new AddPick(kind, false)).ToList();
                if (Fold(More, left) is { } more)
                {
                    rows.Add(more);
                }
            }

            if (rows.Count > 0)
            {
                columns.Add(new AddColumn(shelf, rows));
            }
        }

        return columns;
    }

    /// <summary>
    /// [S-3513] Every widget the menu shows, once, at its home (never a row shown <see cref="AddPick.Again"/>),
    /// with the folders it is in below its top column: what a search lists, and the path a result shows.
    /// </summary>
    public static IReadOnlyList<AddHome> Homes(IReadOnlyList<AddColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var homes = new List<AddHome>();
        foreach (var column in columns)
        {
            Walk(column.Rows, [], homes);
        }

        return homes;
    }

    private static void Walk(IReadOnlyList<AddRow> rows, List<AddShelf> path, List<AddHome> homes)
    {
        foreach (var row in rows)
        {
            switch (row)
            {
                case AddPick { Again: false } pick:
                    homes.Add(new AddHome(pick.Kind, [.. path]));
                    break;
                case AddFolder folder:
                    path.Add(folder.Shelf);
                    Walk(folder.Rows, path, homes);
                    path.RemoveAt(path.Count - 1);
                    break;
            }
        }
    }

    private static List<AddRow> Resolve(IReadOnlyList<AddNode> nodes, HashSet<WidgetId> have, HashSet<WidgetId> placed)
    {
        var rows = new List<AddRow>();
        foreach (var node in nodes)
        {
            switch (node)
            {
                case AddLeaf leaf when have.Contains(leaf.Kind):
                    if (!leaf.Again)
                    {
                        placed.Add(leaf.Kind);
                    }

                    rows.Add(new AddPick(leaf.Kind, leaf.Again));
                    break;
                case AddLine:
                    rows.Add(new AddRule());
                    break;
                case AddShelf shelf when Fold(shelf, Resolve(shelf.Rows, have, placed)) is { } row:
                    rows.Add(row);
                    break;
            }
        }

        return Tidy(rows);
    }

    /// <summary>A folder of two rows or more; one row is that row; none, nothing.</summary>
    private static AddRow? Fold(AddShelf shelf, List<AddRow> rows)
    {
        rows = Tidy(rows);
        var picks = rows.Count(row => row is not AddRule);
        return picks switch
        {
            0 => null,
            1 => rows.First(row => row is not AddRule),
            _ => new AddFolder(shelf, rows),
        };
    }

    /// <summary>A line only between two rows: none first, last or beside another.</summary>
    private static List<AddRow> Tidy(List<AddRow> rows)
    {
        var tidy = new List<AddRow>(rows.Count);
        foreach (var row in rows)
        {
            if (row is AddRule && (tidy.Count == 0 || tidy[^1] is AddRule))
            {
                continue;
            }

            tidy.Add(row);
        }

        while (tidy.Count > 0 && tidy[^1] is AddRule)
        {
            tidy.RemoveAt(tidy.Count - 1);
        }

        return tidy;
    }
}
