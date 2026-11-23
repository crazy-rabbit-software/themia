using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-4400] [S-16970] Windows' notification list on the item base: how many notifications a new card stands, their arrangement,
/// gap and parts, and whether they stand together by app.
/// </summary>
public sealed class NotificationsConfig : WidgetConfig, IKitLocalConfig, IItemList, IStyled
{
    /// <summary>[S-11404] A new notification list's rows.</summary>
    public const int DefaultRows = 4;

    /// <summary>[S-11404] How many notifications a new card stands; more scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-16970] The notifications together by app, the app with the newest first (off: newest first).</summary>
    public bool ByApp { get; set; }

    /// <summary>[S-16940] The item base's arrangement, the list first.</summary>
    public ItemStyle Style { get; set; } = ItemStyle.List;

    /// <summary>[S-16673] The grid blocks between two notifications.</summary>
    public int Gap { get; set; }

    /// <summary>[S-16952] A table's column titles.</summary>
    public bool ColumnTitles { get; set; } = true;

    /// <summary>[S-16946] The parts left out: the app's name (its icon says it) by default.</summary>
    public List<ItemPart> HiddenParts { get; set; } = [ItemPart.Note];

    /// <summary>[S-16950] The parts' order: a table's columns.</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

    [JsonIgnore]
    ItemStyle IItemList.Arrangement => Style;

    IReadOnlyList<StyleOption> IStyled.Styles => ItemStyles.All;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ItemStyle)value;
    }

    /// <summary>[S-16970] One face a notification: no lines to choose.</summary>
    [JsonIgnore]
    bool IKitRows.OffersLines => false;

    [JsonIgnore]
    public int Lines
    {
        get => 1;
        set
        {
            // ignore: an item has its one face; the menu offers no lines.
        }
    }

    /// <summary>[S-11404] The icon, the body and when it came are the item's.</summary>
    [JsonIgnore]
    public KitShow Shows => KitShow.Lead | KitShow.Subtitle | KitShow.When;

    public override IReadOnlyList<string> Repair() => ItemOrder.Repair(this, "notifications");

    public override WidgetConfig Clone() => new NotificationsConfig
    {
        Rows = Rows,
        ByApp = ByApp,
        Style = Style,
        Gap = Gap,
        ColumnTitles = ColumnTitles,
        HiddenParts = [.. HiddenParts],
        PartOrder = [.. PartOrder],
    };
}
