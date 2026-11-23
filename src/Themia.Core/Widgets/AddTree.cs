using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>One row of the declared tree: a widget, a folder of rows, or a line.</summary>
public abstract record AddNode;

/// <summary>
/// A widget. <paramref name="Again"/>: shown here once more, its home being elsewhere in the tree
/// (Popular's, [S-3511]); search lists it at its home only. [S-16681] <paramref name="Preset"/>: one of the kind's
/// presets, offered as a widget of its own.
/// </summary>
public sealed record AddLeaf(WidgetId Kind, bool Again = false, string? Preset = null) : AddNode
{
    /// <summary>[S-16681] What it offers: the kind, or its preset.</summary>
    public WidgetPick Pick => new(Kind, Preset);
}

/// <summary>A line between two runs of a folder's rows: the order the runs come in.</summary>
public sealed record AddLine : AddNode;

/// <summary>A folder: a subject (an item of the add page's rail) or a family inside one (a stack of its widgets).</summary>
/// <param name="Icon">The widget whose glyph stands for it: its first widget's.</param>
public sealed record AddShelf(AddFolderId Id, LocalText Title, WidgetPick Icon, IReadOnlyList<AddNode> Rows) : AddNode;

/// <summary>
/// [S-3513] A widget's home: the folders it is in, outermost first (its subject, then its family when it has one).
/// [S-16681] <paramref name="Preset"/>: a preset of the kind, at home on its own.
/// </summary>
public sealed record AddHome(WidgetId Kind, IReadOnlyList<AddShelf> Path, string? Preset = null)
{
    /// <summary>[S-16681] What is at home here: the kind, or its preset.</summary>
    public WidgetPick Pick => new(Kind, Preset);
}

/// <summary>
/// [S-3510] [S-3511] The add page's tree, after Karpo's add list (<c>karpo-app</c>, docs/widgets.md
/// §136): it answers the one question a person adding a widget has — what do I want? — by subject.
/// <b>Popular</b> (a fixed handful, each again from its home) and <b>Browse</b> (a folder per subject);
/// inside a subject a <b>family</b> — one widget in several parts (a reading's meter and its details,
/// the seven power buttons) — is a folder of its own. Never deeper than that. The add page
/// (<see cref="AddPage"/>) draws it: a subject an item of its rail, a family a stack of its widgets ([S-13610], [S-13612]).
/// <para>
/// The folders are declared here; each widget places itself in one through its descriptor
/// (<see cref="WidgetDescriptor.Add"/>, [S-6907]). A kind the tree has no home for is still listed,
/// in <see cref="AddFolderId.More"/>, while <c>AddMenuTests</c> fail until it has one.
/// </para>
/// </summary>
public static class AddTree
{
    private static readonly AddLine Line = new();

    /// <summary>A folder of the tree; a null title is its first widget's name (a family named after its reading, [S-3511]).</summary>
    private sealed record Folder(AddFolderId Id, Str? Title, AddFolderId Parent, int Group, int Rank);

    /// <summary>
    /// [S-3511] The tree's folders: the subjects under Browse, in the order a person looks for them,
    /// and the families inside a subject (a reading's meter and its history, the screen time parts,
    /// the power buttons, the single metrics of the weather, the air, the sky and each system reading, [S-14604]). The folders are the host's; the widgets place themselves in them ([S-6907],
    /// <see cref="AddPlacement"/>), so this names no widget.
    /// </summary>
    private static readonly Folder[] Folders =
    [
        new(AddFolderId.Time, Str.AddMenu_Subject_Time, AddFolderId.Browse, 0, 0),
        new(AddFolderId.CalendarMail, Str.AddMenu_Subject_Calendar, AddFolderId.Browse, 0, 1),
        new(AddFolderId.Weather, Str.AddMenu_Subject_Weather, AddFolderId.Browse, 0, 2),
        new(AddFolderId.WeatherFacts, Str.AddMenu_Family_WeatherFacts, AddFolderId.Weather, 0, 1),
        new(AddFolderId.AirFacts, Str.AddMenu_Family_AirFacts, AddFolderId.Weather, 0, 5),
        new(AddFolderId.SunTimes, Str.AddMenu_Family_SunTimes, AddFolderId.Weather, 1, 1),
        new(AddFolderId.MoonTimes, Str.AddMenu_Family_MoonTimes, AddFolderId.Weather, 1, 3),
        new(AddFolderId.Markets, Str.AddMenu_Subject_Markets, AddFolderId.Browse, 0, 3),
        new(AddFolderId.Media, Str.AddMenu_Subject_Media, AddFolderId.Browse, 0, 4),
        new(AddFolderId.Notes, Str.AddMenu_Subject_Notes, AddFolderId.Browse, 0, 5),
        new(AddFolderId.Files, Str.AddMenu_Subject_Files, AddFolderId.Browse, 0, 6),
        new(AddFolderId.System, Str.AddMenu_Subject_System, AddFolderId.Browse, 0, 7),
        new(AddFolderId.Processor, null, AddFolderId.System, 0, 0),
        new(AddFolderId.CpuFacts, Str.AddMenu_Family_CpuFacts, AddFolderId.System, 0, 0),
        new(AddFolderId.MemoryFacts, Str.AddMenu_Family_MemoryFacts, AddFolderId.System, 0, 1),
        new(AddFolderId.Graphics, null, AddFolderId.System, 0, 2),
        new(AddFolderId.GpuFacts, Str.AddMenu_Family_GpuFacts, AddFolderId.System, 0, 2),
        new(AddFolderId.NetworkReading, null, AddFolderId.System, 0, 4),
        new(AddFolderId.NetworkFacts, Str.AddMenu_Family_NetworkFacts, AddFolderId.System, 0, 4),
        new(AddFolderId.PingFacts, Str.AddMenu_Family_PingFacts, AddFolderId.System, 0, 5),
        new(AddFolderId.ScreenTime, Str.AddMenu_Family_ScreenTime, AddFolderId.System, 1, 0),
        new(AddFolderId.Devices, Str.AddMenu_Subject_Devices, AddFolderId.Browse, 0, 8),
        new(AddFolderId.BatteryFacts, Str.AddMenu_Family_BatteryFacts, AddFolderId.Devices, 0, 0),
        new(AddFolderId.DriveFacts, Str.AddMenu_Family_DriveFacts, AddFolderId.Devices, 2, 1),
        new(AddFolderId.PowerButtons, Str.AddMenu_Family_PowerButtons, AddFolderId.Devices, 0, 1),
        new(AddFolderId.QuickSettings, Str.Toggles_Category, AddFolderId.Browse, 0, 9),
    ];

    private static readonly Lazy<(AddShelf Popular, AddShelf Browse)> Tree = new(() => Build(ModuleCatalog.Widgets));

    /// <summary>
    /// [S-3511] What most people add first, each again where it is at home, in the order of how often
    /// it is wanted (each widget's <see cref="AddPlacement.Popular"/>): Start's Popular.
    /// </summary>
    public static AddShelf Popular => Tree.Value.Popular;

    /// <summary>[S-3511] Every subject, a folder each, in the rail's order. Inside each, the most wanted first.</summary>
    public static AddShelf Browse => Tree.Value.Browse;

    /// <summary>
    /// [S-3511] Every toggle, as Windows' quick settings have them, lines parting them by subject: the
    /// subject a person looks for one of them in by its name.
    /// </summary>
    public static AddShelf QuickSettings => Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == AddFolderId.QuickSettings);

    /// <summary>The tree's top: Popular and Browse.</summary>
    public static IReadOnlyList<AddShelf> Top => [Popular, Browse];

    /// <summary>
    /// [S-11705] The subject <paramref name="folder"/> is in: itself for a subject, its parent for a
    /// family; null for Start, Popular, Browse and More, which are no subject.
    /// </summary>
    public static AddFolderId? SubjectOf(AddFolderId folder)
    {
        for (var at = Folders.FirstOrDefault(f => f.Id == folder); at is not null; at = Folders.FirstOrDefault(f => f.Id == at.Parent))
        {
            if (at.Parent == AddFolderId.Browse)
            {
                return at.Id;
            }
        }

        return null;
    }

    /// <summary>Popular and Browse over <paramref name="widgets"/>: the catalogue's, or a part of it in a test.</summary>
    internal static (AddShelf Popular, AddShelf Browse) Build(IReadOnlyList<WidgetDescriptor> widgets)
    {
        // [S-16681] A kind with presets is offered as them, each with its own place.
        List<(AddPlacement Add, AddLeaf Leaf)> picks = [.. widgets.SelectMany(w => w.Presets.Count == 0
            ? Enumerable.Repeat((w.Add, new AddLeaf(w.Id)), 1)
            : w.Presets.Select(p => (p.Add, new AddLeaf(w.Id, Preset: p.Key))))];
        AddLeaf[] popular = [.. picks.Where(p => p.Add.Popular is not null).OrderBy(p => p.Add.Popular).Select(p => p.Leaf with { Again = true })];
        return (new AddShelf(AddFolderId.Popular, Str.AddMenu_Popular.Local(), popular[0].Pick, popular),
            Shelf(AddFolderId.Browse, Str.AddMenu_Browse, picks) ?? throw new InvalidOperationException("a build with no widget in it"));
    }

    /// <summary>
    /// A folder's rows: its widgets and its folders, by run and place, a line between two runs. Null
    /// for a folder no widget of the build is at home in (every one of its modules absent): it is not
    /// in the tree ([S-6907]).
    /// </summary>
    private static AddShelf? Shelf(AddFolderId id, Str? title, IReadOnlyList<(AddPlacement Add, AddLeaf Leaf)> picks)
    {
        var entries = picks.Where(p => p.Add.Shelf == id).Select(p => (p.Add.Group, p.Add.Rank, Node: (AddNode?)p.Leaf))
            .Concat(Folders.Where(f => f.Parent == id).Select(f => (f.Group, f.Rank, Node: (AddNode?)Shelf(f.Id, f.Title, picks))))
            .Where(e => e.Node is not null)
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
            rows.Add(node!);
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var icon = IconOf(rows);
        return new AddShelf(id, title?.Local() ?? icon.Label, icon, rows);
    }

    /// <summary>A folder wears its first widget's icon.</summary>
    private static WidgetPick IconOf(IReadOnlyList<AddNode> rows) => rows.FirstOrDefault(row => row is not AddLine) switch
    {
        AddLeaf leaf => leaf.Pick,
        AddShelf shelf => shelf.Icon,
        _ => throw new InvalidOperationException("an add page folder with no widget in it"),
    };
}
