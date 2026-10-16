using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-3510] Every folder of the add menu's tree, by name: the two top columns, the subjects under
/// Browse, and the families inside a subject. A closed set, so a folder's words and a debug render's
/// <c>--open</c> name one that exists.
/// </summary>
public enum AddFolderId
{
    Popular,
    Browse,

    // The subjects, in Browse's order.
    Time,
    CalendarMail,
    Weather,
    Markets,
    Media,
    Notes,
    Files,
    System,
    Devices,
    QuickSettings,

    /// <summary>[S-3511] What the tree leaves out stands here meanwhile, at Browse's end: never missing.</summary>
    More,

    // The families: one widget in several layouts or several parts.
    CalendarLayouts,
    Visualizers,
    FolderLayouts,
    Processor,
    Graphics,
    MemoryReading,
    NetworkReading,
    PingReading,
    ScreenTime,
    BatteryReading,
    PowerButtons,
}

/// <summary>One row of the declared tree: a widget, a folder of rows, or a line.</summary>
public abstract record AddNode;

/// <summary>
/// A widget. <paramref name="Again"/>: shown here once more, its home being elsewhere in the tree
/// (Popular's rows, [S-3511]); search lists it at its home only.
/// </summary>
public sealed record AddLeaf(WidgetKind Kind, bool Again = false) : AddNode;

/// <summary>A line between two runs of a folder's rows.</summary>
public sealed record AddLine : AddNode;

/// <summary>A folder: a row opening a submenu of its rows; at the top, a column under its heading.</summary>
/// <param name="Icon">The kind whose icon its row wears; ignored for a top column, which is a heading.</param>
public sealed record AddShelf(AddFolderId Id, Str Title, WidgetKind Icon, IReadOnlyList<AddNode> Rows) : AddNode;

/// <summary>A row of the tree as the menu shows it: a pick, a folder, or a line.</summary>
public abstract record AddRow;

/// <summary>A row that adds <see cref="Kind"/>; <see cref="Again"/> as <see cref="AddLeaf.Again"/>.</summary>
public sealed record AddPick(WidgetKind Kind, bool Again) : AddRow;

/// <summary>A folder as the menu shows it: always two rows or more.</summary>
public sealed record AddFolder(AddShelf Shelf, IReadOnlyList<AddRow> Rows) : AddRow;

/// <summary>A line as the menu shows it: always between two rows.</summary>
public sealed record AddRule : AddRow;

/// <summary>One of the menu's top columns: its heading's shelf and its rows.</summary>
public sealed record AddColumn(AddShelf Shelf, IReadOnlyList<AddRow> Rows);

/// <summary>A widget's home in the tree: the folders it is in under its top column, outermost first.</summary>
public sealed record AddHome(WidgetKind Kind, IReadOnlyList<AddShelf> Path);

/// <summary>
/// [S-3510..S-3512] The add menu's tree, after Karpo's add list (<c>karpo-app</c>, docs/widgets.md
/// §136): the menu asks the one question a person adding a widget has — what do I want? — and
/// answers it by subject. Two top columns, <b>Popular</b> (a fixed handful, each again from its home)
/// and <b>Browse</b> (a row per subject, opening its submenu as Windows' "New ›" does); inside a
/// subject a <b>family</b> — one widget in several layouts or parts (the calendar's five layouts,
/// the visualizer's five designs, a reading's meter and its details, the seven power buttons) — is
/// one row opening its own submenu. Never deeper than that.
/// <para>
/// Declared here as data, by <see cref="WidgetKind"/>; <see cref="On"/> resolves it against what the
/// catalogue offers, so a kind added to the catalogue and not to the tree is still listed, in
/// <see cref="AddFolderId.More"/>, while <c>AddTreeTests</c> fail until it has a home.
/// </para>
/// </summary>
public static class AddTree
{
    /// <summary>[S-3512] The most rows a column or a folder holds, a line counted as a row; Quick settings alone is longer.</summary>
    public const int MostRows = 12;

    private static AddLeaf W(WidgetKind kind) => new(kind);

    private static AddLeaf Again(WidgetKind kind) => new(kind, Again: true);

    private static readonly AddLine Line = new();

    private static AddShelf Shelf(AddFolderId id, Str title, WidgetKind icon, params AddNode[] rows) => new(id, title, icon, rows);

    /// <summary>A reading's family: its meter, its details and what else is the reading's.</summary>
    private static AddShelf Reading(AddFolderId id, Str title, params WidgetKind[] kinds) =>
        Shelf(id, title, kinds[0], [.. kinds.Select(W)]);

    /// <summary>
    /// [S-3511] Every toggle, as Windows' quick settings have them, lines parting them by subject —
    /// the radios, the sound, the screen, the PC. The one folder longer than <see cref="MostRows"/>,
    /// since it is every toggle and a person looks for one of them by its name.
    /// </summary>
    public static readonly AddShelf QuickSettings = Shelf(AddFolderId.QuickSettings, Str.Toggles_Category, WidgetKind.WifiToggle,
        W(WidgetKind.WifiToggle), W(WidgetKind.BluetoothToggle), W(WidgetKind.AirplaneModeToggle),
        Line,
        W(WidgetKind.SoundToggle), W(WidgetKind.MicrophoneToggle), W(WidgetKind.SpatialSoundToggle), W(WidgetKind.MonoAudioToggle),
        Line,
        W(WidgetKind.NightLightToggle), W(WidgetKind.ColorFiltersToggle), W(WidgetKind.RotationLockToggle),
        Line,
        W(WidgetKind.EnergySaverToggle), W(WidgetKind.DoNotDisturbToggle), W(WidgetKind.GameModeToggle));

    /// <summary>
    /// [S-3511] The first column: what most people add first, each again where it is at home, in
    /// the order of how often it is wanted.
    /// </summary>
    public static readonly AddShelf Popular = Shelf(AddFolderId.Popular, Str.AddMenu_Popular, WidgetKind.Clock,
        Again(WidgetKind.Clock),
        Again(WidgetKind.WeatherNow),
        Again(WidgetKind.WeatherForecast),
        Again(WidgetKind.MonthCalendar),
        Again(WidgetKind.Agenda),
        Again(WidgetKind.Email),
        Again(WidgetKind.Tasks),
        Again(WidgetKind.Notes),
        Again(WidgetKind.Music),
        Again(WidgetKind.Cpu),
        Again(WidgetKind.Folder),
        Again(WidgetKind.Shortcuts));

    /// <summary>
    /// [S-3511] The second column: every subject, a folder each, in the order a person looks for
    /// them. Inside each, the most wanted first.
    /// </summary>
    public static readonly AddShelf Browse = Shelf(AddFolderId.Browse, Str.AddMenu_Browse, WidgetKind.Clock,
        Shelf(AddFolderId.Time, Str.AddMenu_Subject_Time, WidgetKind.Clock,
            W(WidgetKind.Clock), W(WidgetKind.Alarm), W(WidgetKind.Countdown), W(WidgetKind.Stopwatch),
            Line,
            W(WidgetKind.Pomodoro), W(WidgetKind.FocusSession),
            Line,
            W(WidgetKind.YearProgress)),
        Shelf(AddFolderId.CalendarMail, Str.AddMenu_Subject_Calendar, WidgetKind.MonthCalendar,
            Shelf(AddFolderId.CalendarLayouts, Str.AddMenu_Family_Calendar, WidgetKind.MonthCalendar,
                W(WidgetKind.MonthCalendar), W(WidgetKind.Agenda), W(WidgetKind.WeekCalendar), W(WidgetKind.DayCalendar), W(WidgetKind.YearCalendar)),
            W(WidgetKind.NextMeeting), W(WidgetKind.WeekPlanner),
            Line,
            W(WidgetKind.Email), W(WidgetKind.Tasks), W(WidgetKind.Contacts),
            Line,
            W(WidgetKind.Notifications)),
        Shelf(AddFolderId.Weather, Str.AddMenu_Subject_Weather, WidgetKind.WeatherNow,
            W(WidgetKind.WeatherNow), W(WidgetKind.WeatherForecast), W(WidgetKind.RainNextHour), W(WidgetKind.AirQuality),
            Line,
            W(WidgetKind.Sun), W(WidgetKind.Moon)),
        Shelf(AddFolderId.Markets, Str.AddMenu_Subject_Markets, WidgetKind.Stocks,
            W(WidgetKind.Stocks), W(WidgetKind.ExchangeRates),
            Line,
            W(WidgetKind.Rss)),
        Shelf(AddFolderId.Media, Str.AddMenu_Subject_Media, WidgetKind.Music,
            W(WidgetKind.Music),
            Shelf(AddFolderId.Visualizers, Str.AddMenu_Family_Visualizer, WidgetKind.AudioVisualizer,
                W(WidgetKind.AudioVisualizer), W(WidgetKind.VisualizerMirror), W(WidgetKind.VisualizerWave), W(WidgetKind.VisualizerDots), W(WidgetKind.VisualizerRadial), W(WidgetKind.VisualizerScope)),
            W(WidgetKind.Volume), W(WidgetKind.Mixer),
            Line,
            W(WidgetKind.Gallery), W(WidgetKind.VideoStream)),
        Shelf(AddFolderId.Notes, Str.AddMenu_Subject_Notes, WidgetKind.Notes,
            W(WidgetKind.Notes), W(WidgetKind.Scratchpad), W(WidgetKind.Clipboard),
            Line,
            W(WidgetKind.HabitTracker), W(WidgetKind.Quote)),
        Shelf(AddFolderId.Files, Str.AddMenu_Subject_Files, WidgetKind.Folder,
            Shelf(AddFolderId.FolderLayouts, Str.AddMenu_Family_Folder, WidgetKind.Folder,
                W(WidgetKind.Folder), W(WidgetKind.FolderDetails), W(WidgetKind.FolderGrid), W(WidgetKind.FolderIcons)),
            W(WidgetKind.Shortcuts), W(WidgetKind.RecentFiles), W(WidgetKind.RecycleBin)),
        Shelf(AddFolderId.System, Str.AddMenu_Subject_System, WidgetKind.Cpu,
            Reading(AddFolderId.Processor, Str.WidgetsA_Catalog_Cpu_Label, WidgetKind.Cpu, WidgetKind.CpuDetails, WidgetKind.TopProcesses),
            Reading(AddFolderId.MemoryReading, Str.WidgetsA_Catalog_Memory_Label, WidgetKind.Memory, WidgetKind.MemoryDetails),
            Reading(AddFolderId.Graphics, Str.WidgetsA_Catalog_Gpu_Label, WidgetKind.Gpu, WidgetKind.GpuDetails),
            W(WidgetKind.Disk),
            Reading(AddFolderId.NetworkReading, Str.WidgetsA_Catalog_Network_Label, WidgetKind.Network, WidgetKind.NetworkDetails),
            Reading(AddFolderId.PingReading, Str.Widget_Ping_Label, WidgetKind.Ping, WidgetKind.PingDetails),
            Line,
            Shelf(AddFolderId.ScreenTime, Str.AddMenu_Family_ScreenTime, WidgetKind.ScreenTimeChart,
                W(WidgetKind.ScreenTimeChart), W(WidgetKind.ScreenTimeDonut), W(WidgetKind.ScreenTimeApps))),
        Shelf(AddFolderId.Devices, Str.AddMenu_Subject_Devices, WidgetKind.Battery,
            Reading(AddFolderId.BatteryReading, Str.Widget_Battery_Label, WidgetKind.Battery, WidgetKind.BatteryDetails),
            Shelf(AddFolderId.PowerButtons, Str.AddMenu_Family_PowerButtons, WidgetKind.Shutdown,
                W(WidgetKind.Shutdown), W(WidgetKind.Restart), W(WidgetKind.Sleep), W(WidgetKind.Hibernate),
                W(WidgetKind.Lock), W(WidgetKind.SignOut), W(WidgetKind.ScreenOff)),
            Line,
            W(WidgetKind.Brightness), W(WidgetKind.Bluetooth),
            Line,
            W(WidgetKind.Drive), W(WidgetKind.RemovableDrives)),
        QuickSettings);

    /// <summary>The menu's top: its two columns, left to right.</summary>
    public static IReadOnlyList<AddShelf> Top => [Popular, Browse];

    /// <summary>Where what the tree leaves out stands meanwhile ([S-3511]); its rows are filled by <see cref="On"/>.</summary>
    private static readonly AddShelf More = Shelf(AddFolderId.More, Str.AddMenu_Subject_More, WidgetKind.Clock);

    /// <summary>
    /// [S-3511] What the menu shows of the tree for the kinds the catalogue offers, in the
    /// catalogue's order: each top column with its rows, a folder only where it keeps two rows or more
    /// (one is that row; none, no folder), a line only between two rows. An offered kind the tree has
    /// no home for stands in <see cref="AddFolderId.More"/>, at Browse's end.
    /// </summary>
    public static IReadOnlyList<AddColumn> On(IReadOnlyList<WidgetKind> offered)
    {
        ArgumentNullException.ThrowIfNull(offered);

        var have = new HashSet<WidgetKind>(offered);
        var placed = new HashSet<WidgetKind>();
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

    private static List<AddRow> Resolve(IReadOnlyList<AddNode> nodes, HashSet<WidgetKind> have, HashSet<WidgetKind> placed)
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
