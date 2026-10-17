using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-1380] [S-6401] How the Shortcuts widget lays its items out (original S-22): its styles, what
/// was its View setting.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<ShortcutsStyle>))]
public enum ShortcutsStyle
{
    /// <summary>Tiles: the icon with the name centred under it. The original's default.</summary>
    Grid = 0,

    /// <summary>Rows: icon and name.</summary>
    List = 1,

    /// <summary>A table: icon and name, then the target, then what kind of thing it is.</summary>
    Details = 2,
}

/// <summary>[S-6401] [S-6074] The shortcuts' rows that only some styles or states draw with.</summary>
public enum ShortcutsSetting
{
    /// <summary>The grid's tile size.</summary>
    TileSize,

    /// <summary>The grid's icons-only switch.</summary>
    IconOnly,

    /// <summary>The grid's stretch to the height.</summary>
    Stretch,

    /// <summary>The list and details row size.</summary>
    RowSize,

    /// <summary>File extensions on the names: not on a grid of icons alone, which draws no names.</summary>
    Extensions,
}

/// <summary>[S-1380] How big a tile or a row is drawn (original S-23 / S-46).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShortcutItemSize>))]
public enum ShortcutItemSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

/// <summary>
/// One thing the user keeps to hand: a web address, a file or folder, or a Windows location.
/// <para>
/// The <see cref="Target"/> is untrusted. It reaches here from a text box and from a JSON file in
/// the user's profile that anything running as the user can rewrite, and it ends at a shell call —
/// so it is checked by <see cref="LaunchTargets"/> every time it is used, never once on the way in.
/// A config that was edited after it was saved gets the same suspicion as one that was typed.
/// </para>
/// <para>
/// [S-1405] An item with a <see cref="Layout"/> is an <b>app layout</b>: activating it opens every
/// app of its pane tree and puts each window in its pane. Its <see cref="Target"/> is empty and
/// never launched.
/// </para>
/// </summary>
public sealed class ShortcutItem
{
    /// <summary>Stable id, so reordering and editing cannot swap two shortcuts' settings.</summary>
    public required string Id { get; set; }

    /// <summary>The user's own name for it, or empty to use the target's own.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Where it points. Never launched without being classified first.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// [S-1401] The icon a dropped <c>.url</c> named (<c>IconFile=</c>, a Steam game's icon), drawn
    /// instead of the target's own. A local path, gated like every probe; null for none.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>[S-1405] The app layout's pane tree, or null for an ordinary shortcut.</summary>
    public LayoutNode? Layout { get; set; }

    /// <summary>Whether this item is an app layout rather than a target.</summary>
    [JsonIgnore]
    public bool IsLayout => Layout is not null;

    /// <summary>[S-1405] A new app layout: one empty pane, named by the user later (original S-55).</summary>
    public static ShortcutItem CreateLayout(string label = "") => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Label = label,
        Layout = LayoutNode.NewPane(),
    };

    public static ShortcutItem Create(string target, string label = "") => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Target = target,
        Label = label,
    };

    /// <summary>A copy with a new id, so a duplicated shortcut is a separate row.</summary>
    public ShortcutItem Clone() => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Label = Label,
        Target = Target,
        IconPath = IconPath,
        Layout = Layout?.Clone(),
    };
}

/// <summary>
/// An ordered list of shortcuts and how to draw them.
/// </summary>
public sealed class ShortcutsConfig : WidgetConfig, IStyled, IShownSettings<ShortcutsSetting>, ISetupRule
{
    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(AccountFeatures signedIn) => Items.Count == 0;

    /// <summary>[S-1881] Its setup is a drop onto the widget itself, so it runs while it waits.</summary>
    bool ISetupRule.SetsUpInPlace => true;

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Shortcuts;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("shortcuts"),
        Kind = WidgetKind.Shortcuts,
        ConfigType = typeof(ShortcutsConfig),
        CreateDefault = ShortcutsConfig.CreateDefault,
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["link", "launcher", "bookmark", "url", "open", "pin", "quick access"],
        LabelId = Str.WidgetsA_Catalog_Shortcuts_Label,
        DescriptionId = Str.WidgetsA_Catalog_Shortcuts_Description,
        Glyph = Glyphs.Shortcut,
        DefaultWidth = 320,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Files, 0, 1, Popular: 10),
    };

    public List<ShortcutItem> Items { get; set; } = [];

    /// <summary>[S-1380] [S-6401] Grid (default), list or details: the widget's style.</summary>
    public ShortcutsStyle Style { get; set; } = ShortcutsStyle.Grid;

    private static readonly StyleOption[] ShortcutsStyles =
    [
        StyleOption.Of(ShortcutsStyle.Grid, Str.WidgetsC_Shortcuts_Settings_View_Tiles, 0xECA5),
        StyleOption.Of(ShortcutsStyle.List, Str.CtxMenu_List, 0xEA37),
        StyleOption.Of(ShortcutsStyle.Details, Str.CtxMenu_Details, 0xF12B),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => ShortcutsStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ShortcutsStyle)value;
    }

    /// <summary>[S-6401] [S-6074] The look settings the current style draws with; the page shows only these.</summary>
    [JsonIgnore]
    public IReadOnlyList<ShortcutsSetting> ShownSettings => Style == ShortcutsStyle.Grid
        ? GridIconOnly
            ? [ShortcutsSetting.TileSize, ShortcutsSetting.IconOnly, ShortcutsSetting.Stretch]
            : [ShortcutsSetting.TileSize, ShortcutsSetting.IconOnly, ShortcutsSetting.Stretch, ShortcutsSetting.Extensions]
        : [ShortcutsSetting.RowSize, ShortcutsSetting.Extensions];

    /// <summary>[S-1380] Tile size in the grid (original <c>gridItemSize</c>).</summary>
    public ShortcutItemSize GridSize { get; set; } = ShortcutItemSize.Medium;

    /// <summary>[S-1380] Row size in the list and details views (original <c>itemSize</c>).</summary>
    public ShortcutItemSize ListSize { get; set; } = ShortcutItemSize.Medium;

    /// <summary>[S-1380] Grid tiles share out the widget's height instead of keeping their own (original S-47).</summary>
    public bool GridStretchHeight { get; set; }

    /// <summary>[S-1380] Grid tiles show only their icon (original S-71).</summary>
    public bool GridIconOnly { get; set; }

    /// <summary>[S-1380] A file's name keeps its extension (original S-48). A label still wins.</summary>
    public bool ShowFileExtension { get; set; }

    /// <summary>
    /// Single or double click to open. Double by default: this surface sits in the pointer's path
    /// across the desktop, and a stray click opening something is what makes a launcher a hazard.
    /// </summary>
    public ItemClickMode ClickMode { get; set; } = ItemClickMode.Double;

    /// <summary>
    /// [S-1393] A new Shortcuts widget starts with the Recycle Bin and This PC (original S-20), the
    /// two desktop icons a widget desktop most often replaces.
    /// </summary>
    public static ShortcutsConfig CreateDefault() => new()
    {
        Items =
        [
            ShortcutItem.Create(LaunchTargets.ShellTarget("RecycleBinFolder")),
            ShortcutItem.Create(LaunchTargets.ShellTarget("MyComputerFolder")),
        ],
    };

    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        // A hand-edited "items": null, or a null entry in the list, is corrupt rather than empty.
        Items ??= [];

        if (Items.RemoveAll(item => item is null || item.Target is null || item.Label is null) > 0)
        {
            repairs.Add("Shortcuts widget had a broken item");
        }

        if (!Enum.IsDefined(GridSize))
        {
            repairs.Add("Shortcuts widget had an unknown tile size");
            GridSize = ShortcutItemSize.Medium;
        }

        if (!Enum.IsDefined(ListSize))
        {
            repairs.Add("Shortcuts widget had an unknown row size");
            ListSize = ShortcutItemSize.Medium;
        }

        if (!Enum.IsDefined(ClickMode))
        {
            repairs.Add("Shortcuts widget had an unknown click mode");
            ClickMode = ItemClickMode.Double;
        }

        foreach (var item in Items)
        {
            // [S-1406] A layout's tree is made honest; its target is never launched, so it is blank.
            if (item.Layout is not null)
            {
                item.Layout = LayoutTree.Repair(item.Layout, repairs);

                if (item.Target.Length > 0)
                {
                    item.Target = string.Empty;
                    repairs.Add("Shortcuts widget had an app layout with a target");
                }
            }

            if (item.IconPath is { } icon && LaunchTargets.Classify(icon).Kind != LaunchKind.LocalPath)
            {
                item.IconPath = null;
                repairs.Add("Shortcuts widget had an icon that is not a local file");
            }
        }

        // Ids are what selection, rename and remove key on; a hand-edited file with a blank or
        // repeated one would make one click act on two rows.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !seen.Add(item.Id))
            {
                item.Id = Guid.NewGuid().ToString("n");
                seen.Add(item.Id);
                repairs.Add("Shortcuts widget had a missing or repeated item id");
            }
        }

        return repairs;
    }

    public override WidgetConfig Clone()
    {
        var copy = new ShortcutsConfig
        {
            Style = Style,
            GridSize = GridSize,
            ListSize = ListSize,
            GridStretchHeight = GridStretchHeight,
            GridIconOnly = GridIconOnly,
            ShowFileExtension = ShowFileExtension,
            ClickMode = ClickMode,
        };

        foreach (var item in Items)
        {
            copy.Items.Add(item.Clone());
        }

        return copy;
    }
}
