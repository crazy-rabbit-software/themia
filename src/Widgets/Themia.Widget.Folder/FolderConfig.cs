using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Folder;

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
/// [S-4123] How a folder is drawn: rows, a table, labelled tiles, or bare icons. The folder's style
/// ([S-6000]); the first is the default, an unknown stored name is repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<FolderStyle>))]
public enum FolderStyle
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

/// <summary>[S-4121] A style's own line on the folder's properties menu, beside the rows every style shows.</summary>
public enum FolderSetting
{
    RowSize,
    Columns,
    Extensions,
    TileSize,

    /// <summary>Sort by and Reverse; the details table sorts from its column headers instead.</summary>
    Sort,
}

/// <summary>
/// [S-1320] [S-4123] The contents of one folder on this machine, as a small file browser, in one of
/// four styles. What every style uses: the folder, how opening works, the order, and hidden items;
/// then the union of what the styles use ([S-4121]): the row density (list, details), whether names
/// keep their extension (list, details, grid), the columns (details) and the tile size (grid, icons).
/// A style ignores the fields it does not use, and its properties menu shows only its own ([S-4122]).
/// </summary>
public sealed class FolderConfig : WidgetConfig, IStyled, IShownSettings<FolderSetting>
{
    /// <summary>[S-4123] The layout; what were four widget kinds.</summary>
    public FolderStyle Style { get; set; } = FolderStyle.List;

    private static readonly StyleOption[] FolderStyles =
    [
        StyleOption.Of(FolderStyle.List, FolderStr.Styles_Folder_List.Local(), 0xE8B7),
        StyleOption.Of(FolderStyle.Details, FolderStr.Styles_Folder_Details.Local(), 0xF12B),
        StyleOption.Of(FolderStyle.Grid, FolderStr.Styles_Folder_Grid.Local(), 0xECA5),
        StyleOption.Of(FolderStyle.Icons, FolderStr.Styles_Folder_Icons.Local(), 0xF0E2),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => FolderStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (FolderStyle)value;
    }

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

    /// <summary>[S-4121] The row density of the list and the details table.</summary>
    public FolderItemSize ItemSize { get; set; } = FolderItemSize.Medium;

    /// <summary>[S-1321] Off by default, as in the original: "report" rather than "report.docx".</summary>
    public bool ShowExtensions { get; set; }

    /// <summary>[S-4121] The details table's columns after Name, in the order they are drawn.</summary>
    public List<FolderColumn> Columns { get; set; } = [.. FolderColumns.Defaults];

    /// <summary>[S-4121] The tile size of the grid and the icons.</summary>
    public FolderTileSize TileSize { get; set; } = FolderTileSize.Medium;

    /// <summary>
    /// [S-4121] [S-4122] [S-6070] The lines the properties menu shows for the current style, in
    /// menu order: only what that style draws.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<FolderSetting> ShownSettings => Style switch
    {
        FolderStyle.Details => [FolderSetting.RowSize, FolderSetting.Columns, FolderSetting.Extensions],
        FolderStyle.Grid => [FolderSetting.TileSize, FolderSetting.Extensions, FolderSetting.Sort],
        FolderStyle.Icons => [FolderSetting.TileSize, FolderSetting.Sort],
        _ => [FolderSetting.RowSize, FolderSetting.Extensions, FolderSetting.Sort],
    };

    /// <summary>The row density the style draws at; the tile styles have none.</summary>
    [JsonIgnore]
    public FolderItemSize Rows => Style is FolderStyle.List or FolderStyle.Details ? ItemSize : FolderItemSize.Medium;

    /// <summary>The tile size the style draws at; the row styles have none.</summary>
    [JsonIgnore]
    public FolderTileSize Tiles => Style is FolderStyle.Grid or FolderStyle.Icons ? TileSize : FolderTileSize.Small;

    /// <summary>[S-1321] Whether names keep their extension; the icons draw no names.</summary>
    [JsonIgnore]
    public bool Extensions => Style != FolderStyle.Icons && ShowExtensions;

    /// <summary>The details columns after Name; only the details table has any.</summary>
    [JsonIgnore]
    public IReadOnlyList<FolderColumn> DetailColumns => Style == FolderStyle.Details ? Columns : [];

    /// <summary>
    /// The watcher ([S-1333]) is the refresh; this is its backstop for a share whose change
    /// notifications never arrive. Slow on purpose: enumerating a directory is disk work.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromSeconds(30);

    /// <summary>[S-1320] The path present; columns known ones, each once, order kept; a hand-edited file is repaired loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        Path ??= string.Empty;

        var (clean, dropped) = FolderColumns.Clean(Columns);

        if (dropped > 0)
        {
            notes.Add($"{Kind}: {dropped} unknown or repeated column(s) dropped");
        }

        Columns = clean;
        return notes;
    }

    public override WidgetConfig Clone() => new FolderConfig
    {
        Style = Style,
        Path = Path,
        Navigation = Navigation,
        ClickMode = ClickMode,
        Sort = Sort,
        Descending = Descending,
        ShowHidden = ShowHidden,
        ItemSize = ItemSize,
        ShowExtensions = ShowExtensions,
        Columns = [.. Columns ?? []],
        TileSize = TileSize,
    };
}
