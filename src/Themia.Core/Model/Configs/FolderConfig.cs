using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>How a folder listing is ordered.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderSort>))]
public enum FolderSort
{
    Name = 0,
    Modified = 1,
    Size = 2,
}

/// <summary>
/// [S-4120] How a folder is drawn: rows, a table, labelled tiles, or bare icons. Never persisted: each
/// layout is its own widget kind and <see cref="FolderConfig.View"/> follows from the kind.
/// </summary>
public enum FolderViewMode
{
    List = 0,
    Details = 1,
    Grid = 2,
    Icons = 3,
}

/// <summary>[S-1320] Row density of the list and details views.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderItemSize>))]
public enum FolderItemSize
{
    ExtraSmall = 0,
    Small = 1,
    Medium = 2,
    Large = 3,
}

/// <summary>[S-1320] Tile size of the grid and icons views.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderTileSize>))]
public enum FolderTileSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

/// <summary>[S-1320] What opening a folder does: hand it to Explorer, or show it inside the widget.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderNavigation>))]
public enum FolderNavigation
{
    Explorer = 0,
    Navigate = 1,
}

/// <summary>
/// [S-1320] [S-4120] The contents of one folder on this machine, as a small file browser; the four
/// layouts are four widget kinds. This is what every one of them stores: the folder, how opening
/// works, the order, and hidden items. Each kind adds only what its layout uses ([S-4121]).
/// </summary>
public abstract class FolderConfig : WidgetConfig
{
    /// <summary>[S-4120] The layout this kind draws: fixed by the kind, never stored.</summary>
    [JsonIgnore]
    public abstract FolderViewMode View { get; }

    /// <summary>[S-4120] A fresh config of the kind that draws <paramref name="view"/>.</summary>
    public static FolderConfig For(FolderViewMode view) => view switch
    {
        FolderViewMode.Details => new FolderDetailsConfig(),
        FolderViewMode.Grid => new FolderGridConfig(),
        FolderViewMode.Icons => new FolderIconsConfig(),
        _ => new FolderListConfig(),
    };

    /// <summary>[S-4120] The layout a folder kind draws, or null for any other kind.</summary>
    public static FolderViewMode? ViewOf(WidgetKind kind) => kind switch
    {
        WidgetKind.Folder => FolderViewMode.List,
        WidgetKind.FolderDetails => FolderViewMode.Details,
        WidgetKind.FolderGrid => FolderViewMode.Grid,
        WidgetKind.FolderIcons => FolderViewMode.Icons,
        _ => null,
    };

    /// <summary>
    /// The folder to list. Empty means the user's profile folder, so a newly placed widget shows
    /// something real rather than an error.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    public FolderNavigation Navigation { get; set; } = FolderNavigation.Explorer;

    public ItemClickMode ClickMode { get; set; } = ItemClickMode.Double;

    /// <summary>The order. Details sorts from its column headers; the others from the settings and the header's reverse button.</summary>
    public FolderSort Sort { get; set; } = FolderSort.Name;

    /// <summary>List the newest or largest first, depending on the sort.</summary>
    public bool Descending { get; set; }

    /// <summary>Include items Windows marks hidden or system.</summary>
    public bool ShowHidden { get; set; }

    /// <summary>The row density the layout draws at; the tile layouts have none.</summary>
    [JsonIgnore]
    public virtual FolderItemSize Rows => FolderItemSize.Medium;

    /// <summary>The tile size the layout draws at; the row layouts have none.</summary>
    [JsonIgnore]
    public virtual FolderTileSize Tiles => FolderTileSize.Small;

    /// <summary>[S-1321] Whether names keep their extension; the icons layout draws no names.</summary>
    [JsonIgnore]
    public virtual bool Extensions => false;

    /// <summary>The details columns after Name; only the details layout has any.</summary>
    [JsonIgnore]
    public virtual IReadOnlyList<FolderColumn> DetailColumns => [];

    /// <summary>
    /// The watcher ([S-1333]) is the refresh; this is its backstop for a share whose change
    /// notifications never arrive. Slow on purpose: enumerating a directory is disk work.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromSeconds(30);

    /// <summary>Bring the shared fields back inside their invariants, then the kind's own.</summary>
    public sealed override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        Path ??= string.Empty;
        RepairOwn(notes);
        return notes;
    }

    /// <summary>The kind's own fields, after the shared ones.</summary>
    protected virtual void RepairOwn(List<string> notes)
    {
    }

    /// <summary>Copy the shared fields onto <paramref name="target"/>.</summary>
    protected T CopySharedTo<T>(T target) where T : FolderConfig
    {
        target.Path = Path;
        target.Navigation = Navigation;
        target.ClickMode = ClickMode;
        target.Sort = Sort;
        target.Descending = Descending;
        target.ShowHidden = ShowHidden;
        return target;
    }
}

/// <summary>[S-4121] The list: one row per item, a density, and names with or without extensions.</summary>
public sealed class FolderListConfig : FolderConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Folder;

    [JsonIgnore]
    public override FolderViewMode View => FolderViewMode.List;

    public FolderItemSize ItemSize { get; set; } = FolderItemSize.Medium;

    /// <summary>[S-1321] Off by default, as in the original: "report" rather than "report.docx".</summary>
    public bool ShowExtensions { get; set; }

    [JsonIgnore]
    public override FolderItemSize Rows => ItemSize;

    [JsonIgnore]
    public override bool Extensions => ShowExtensions;

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new FolderListConfig());
        copy.ItemSize = ItemSize;
        copy.ShowExtensions = ShowExtensions;
        return copy;
    }
}

/// <summary>[S-4121] The details table: the list's density and names, and the columns.</summary>
public sealed class FolderDetailsConfig : FolderConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.FolderDetails;

    [JsonIgnore]
    public override FolderViewMode View => FolderViewMode.Details;

    public FolderItemSize ItemSize { get; set; } = FolderItemSize.Medium;

    public bool ShowExtensions { get; set; }

    /// <summary>The columns after Name, in the order they are drawn.</summary>
    public List<FolderColumn> Columns { get; set; } = [.. FolderColumns.Defaults];

    [JsonIgnore]
    public override FolderItemSize Rows => ItemSize;

    [JsonIgnore]
    public override bool Extensions => ShowExtensions;

    [JsonIgnore]
    public override IReadOnlyList<FolderColumn> DetailColumns => Columns;

    /// <summary>[S-1320] Columns: known ones, each once, order kept; a hand-edited file is repaired loudly.</summary>
    protected override void RepairOwn(List<string> notes)
    {
        var (clean, dropped) = FolderColumns.Clean(Columns);

        if (dropped > 0)
        {
            notes.Add($"{Kind}: {dropped} unknown or repeated column(s) dropped");
        }

        Columns = clean;
    }

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new FolderDetailsConfig());
        copy.ItemSize = ItemSize;
        copy.ShowExtensions = ShowExtensions;
        copy.Columns = [.. Columns ?? []];
        return copy;
    }
}

/// <summary>[S-4121] Labelled tiles: a tile size and names with or without extensions.</summary>
public sealed class FolderGridConfig : FolderConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.FolderGrid;

    [JsonIgnore]
    public override FolderViewMode View => FolderViewMode.Grid;

    public FolderTileSize TileSize { get; set; } = FolderTileSize.Medium;

    public bool ShowExtensions { get; set; }

    [JsonIgnore]
    public override FolderTileSize Tiles => TileSize;

    [JsonIgnore]
    public override bool Extensions => ShowExtensions;

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new FolderGridConfig());
        copy.TileSize = TileSize;
        copy.ShowExtensions = ShowExtensions;
        return copy;
    }
}

/// <summary>[S-4121] Bare icons: a tile size and nothing else (no names are drawn).</summary>
public sealed class FolderIconsConfig : FolderConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.FolderIcons;

    [JsonIgnore]
    public override FolderViewMode View => FolderViewMode.Icons;

    public FolderTileSize TileSize { get; set; } = FolderTileSize.Small;

    [JsonIgnore]
    public override FolderTileSize Tiles => TileSize;

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new FolderIconsConfig());
        copy.TileSize = TileSize;
        return copy;
    }
}
